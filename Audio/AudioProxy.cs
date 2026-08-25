using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Diagnostics;
using PlayVoice.Hotkey;
using PlayVoice.Pages.LevelBar;
using PlayVoice.Pages.Workshop;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PlayVoice.Audio;

/// <summary>
/// 音频代理器
/// </summary>
internal class AudioProxy
{
    private const int MaximumSupportedChannelCount = 32;

    public static AudioProxy Inst { get; private set; }

    /// <summary>
    /// 物理麦克风采集
    /// </summary>
    private WasapiCapture physicalMicrophoneCapture;
    /// <summary>
    /// 虚拟扬声器输出（向其播放）=> 虚拟麦克风
    /// </summary>
    private WasapiOut virtualLoudspeakerOutput;
    /// <summary>
    /// 物理扬声器输出
    /// </summary>
    private WasapiOut physicalLoudspeakerOutput;

    private WaveFormat physicalMicrophoneWaveFormat;
    public WaveFormat PhysicalMicrophoneWaveFormat => physicalMicrophoneWaveFormat;


    private WaveFormat physicalLoudspeakerWaveFormat;
    public WaveFormat PhysicalLoudspeakerWaveFormat => physicalLoudspeakerWaveFormat;


    private double audioOutDecibel;
    public double AudioOutDecibel
    {
        get => audioOutDecibel;
        set
        {
            audioOutDecibel = value;
            GlobalData.Inst.Config.AudioOutDecibel = audioOutDecibel;
            GlobalData.Inst.Config.Save();
            if (audioVolumeToVL != null)
                audioVolumeToVL.Volume = (float)AudioData.DecibelToVolume(audioOutDecibel);
        }
    }
    private double audioEarDecibel;
    public double AudioEarDecibel
    {
        get => audioEarDecibel;
        set
        {
            audioEarDecibel = value;
            GlobalData.Inst.Config.AudioEarDecibel = audioEarDecibel;
            GlobalData.Inst.Config.Save();
            if (audioVolumeToPL != null)
                audioVolumeToPL.Volume = (float)AudioData.DecibelToVolume(audioEarDecibel);
        }
    }
    private double microphoneInputDecibel;
    public double MicrophoneInputDecibel
    {
        get => microphoneInputDecibel;
        set
        {
            microphoneInputDecibel = value;
            GlobalData.Inst.Config.MicrophoneInputDecibel = microphoneInputDecibel;
            GlobalData.Inst.Config.Save();
            for (int i = 0; i < physicalMicrophoneVolumeArray.Length; i++)
                if (physicalMicrophoneVolumeArray[i] != null)
                    physicalMicrophoneVolumeArray[i].Volume = (float)AudioData.DecibelToVolume(microphoneInputDecibel);
        }
    }
    private double globalDecibel;
    public double GlobalDecibel
    {
        get => globalDecibel;
        set
        {
            globalDecibel = value;
            GlobalData.Inst.Config.GlobalDecibel = globalDecibel;
            GlobalData.Inst.Config.Save();
            var volume = (float)AudioData.DecibelToVolume(globalDecibel);
            if (physicalLoudspeakerVolume != null)
                physicalLoudspeakerVolume.Volume = volume;
            if (virtualLoudspeakerVolume != null)
                virtualLoudspeakerVolume.Volume = volume;
        }
    }

    private int maxPlayingAudioCount;
    public int MaxPlayingAudioCount
    {
        get => maxPlayingAudioCount;
        set
        {
            int newValue = Math.Clamp(value, 1, 10);
            if (newValue == maxPlayingAudioCount) return;

            maxPlayingAudioCount = newValue;
            GlobalData.Inst.Config.MaxPlayingAudioCount = maxPlayingAudioCount;
            GlobalData.Inst.Config.Save();

            while (AllAudioList.Count > maxPlayingAudioCount)
                AllAudioList[0].Stop();
        }
    }

    public AudioProxy()
    {
        Inst = this;
    }

    public void Init()
    {
        GlobalData.Inst.GoEar_InStateChanged += (value) =>
        {
            if (value)
            {
                physicalMicrophoneBufferArray[1]?.Clear();
                physicalLoudspeakerMixer.AddMixerInput(physicalMicrophoneVolumeArray[1]);
                StopPLEquipmentClear();
            }
            else
            {
                physicalLoudspeakerMixer.RemoveMixerInput(physicalMicrophoneVolumeArray[1]);
                physicalMicrophoneBufferArray[1]?.Clear();
                StopPLEquipmentClear();
            }
        };
        GlobalData.Inst.GoEar_AudioStateChanged += (value) =>
        {
            StopPLEquipmentClear();
        };
        audioOutDecibel = GlobalData.Inst.Config.AudioOutDecibel;
        audioEarDecibel = GlobalData.Inst.Config.AudioEarDecibel;
        microphoneInputDecibel = GlobalData.Inst.Config.MicrophoneInputDecibel;
        globalDecibel = GlobalData.Inst.Config.GlobalDecibel;
        maxPlayingAudioCount = Math.Clamp(GlobalData.Inst.Config.MaxPlayingAudioCount, 1, 10);
        if (maxPlayingAudioCount != GlobalData.Inst.Config.MaxPlayingAudioCount)
        {
            GlobalData.Inst.Config.MaxPlayingAudioCount = maxPlayingAudioCount;
            GlobalData.Inst.Config.Save();
        }
    }


    public bool TrySwitchPhysicalLoudspeaker(
        MMDevice device,
        out Exception exception)
    {
        exception = null;
        if (device == null)
        {
            StopPLEquipment();
            return true;
        }

        WasapiOut preparedOutput = null;
        string deviceID = "<unknown>";
        string stage = "ReadDeviceId";
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            deviceID = device.ID;
            AppLog.Information(
                $"开始初始化物理扬声器。DeviceId={deviceID}",
                "AudioProxy.PhysicalLoudspeaker.Prepare");

            stage = "CreateWasapiOut";
            AppLog.Debug(
                $"创建物理扬声器 WASAPI 输出。DeviceId={deviceID}",
                "AudioProxy.PhysicalLoudspeaker.Prepare");
            preparedOutput = new WasapiOut(
                device,
                AudioClientShareMode.Shared,
                false,
                100);
            preparedOutput.PlaybackStopped += PhysicalLoudspeakerOutput_PlaybackStopped;

            stage = "ValidateMixFormat";
            WaveFormat targetFormat = preparedOutput.OutputWaveFormat;
            WaveFormat preparedWaveFormat = CreatePhysicalLoudspeakerWaveFormat(
                targetFormat,
                deviceID);
            AppLog.Information(
                $"物理扬声器格式已协商。DeviceId={deviceID} | " +
                $"Mix={targetFormat} | Sample={preparedWaveFormat} | " +
                $"ElapsedMs={stopwatch.ElapsedMilliseconds}",
                "AudioProxy.PhysicalLoudspeaker.Format");

            stage = "CreateMixingGraph";
            var preparedMixer = new MixingSampleProvider(preparedWaveFormat)
            {
                ReadFully = true
            };
            var preparedPhysicalVolume = new VolumeSampleProvider(preparedMixer)
            {
                Volume = (float)AudioData.DecibelToVolume(globalDecibel)
            };
            var preparedAudioMixer = new MixingSampleProvider(preparedWaveFormat)
            {
                ReadFully = true
            };
            var preparedAudioVolume = new VolumeSampleProvider(preparedAudioMixer)
            {
                Volume = (float)AudioData.DecibelToVolume(audioEarDecibel)
            };
            var preparedMetering = new MeteringSampleProvider(preparedAudioVolume);
            SetStreamVolume(preparedMetering, SampleEnum.AutioToPL);
            preparedMixer.AddMixerInput(preparedMetering);

            stage = "InitializeWasapiOut";
            AppLog.Debug(
                $"初始化物理扬声器 WASAPI 输出。DeviceId={deviceID}",
                "AudioProxy.PhysicalLoudspeaker.Prepare");
            preparedOutput.Init(preparedPhysicalVolume);

            stage = "StartWasapiOut";
            AppLog.Debug(
                $"启动物理扬声器 WASAPI 输出。DeviceId={deviceID}",
                "AudioProxy.PhysicalLoudspeaker.Prepare");
            preparedOutput.Play();

            stage = "ReplacePreviousOutput";
            StopPLEquipment();
            physicalLoudspeakerWaveFormat = preparedWaveFormat;
            physicalLoudspeakerMixer = preparedMixer;
            physicalLoudspeakerVolume = preparedPhysicalVolume;
            audioMixToPL = preparedAudioMixer;
            audioVolumeToPL = preparedAudioVolume;
            audioToPLMetering = preparedMetering;
            physicalLoudspeakerOutput = preparedOutput;
            preparedOutput = null;
            AppLog.Information(
                $"物理扬声器初始化完成。DeviceId={deviceID} | " +
                $"ElapsedMs={stopwatch.ElapsedMilliseconds}",
                "AudioProxy.PhysicalLoudspeaker.Prepare");
            return true;
        }
        catch (Exception startException)
        {
            exception = new InvalidOperationException(
                $"初始化物理扬声器失败。DeviceId={deviceID} | " +
                $"Stage={stage} | ElapsedMs={stopwatch.ElapsedMilliseconds}",
                startException);
            return false;
        }
        finally
        {
            if (preparedOutput != null)
                preparedOutput.PlaybackStopped -= PhysicalLoudspeakerOutput_PlaybackStopped;
            DisposeOutputSafely(
                preparedOutput,
                "清理初始化失败的物理扬声器输出时出错。",
                "AudioProxy.PhysicalLoudspeaker.Prepare");
        }
    }

    private static WaveFormat CreatePhysicalLoudspeakerWaveFormat(
        WaveFormat targetFormat,
        string deviceID)
    {
        if (targetFormat == null)
        {
            throw new NotSupportedException(
                $"物理扬声器未返回混合格式。DeviceId={deviceID}");
        }
        if (targetFormat.SampleRate <= 0)
        {
            throw new NotSupportedException(
                $"物理扬声器返回了无效采样率。DeviceId={deviceID} | " +
                $"SampleRate={targetFormat.SampleRate} | Format={targetFormat}");
        }
        if (targetFormat.Channels <= 0
            || targetFormat.Channels > MaximumSupportedChannelCount)
        {
            throw new NotSupportedException(
                $"物理扬声器返回了不支持的声道数。DeviceId={deviceID} | " +
                $"Channels={targetFormat.Channels} | Format={targetFormat}");
        }
        if (targetFormat.BlockAlign <= 0
            || targetFormat.AverageBytesPerSecond <= 0)
        {
            throw new NotSupportedException(
                $"物理扬声器返回了无效帧格式。DeviceId={deviceID} | " +
                $"BlockAlign={targetFormat.BlockAlign} | " +
                $"AverageBytesPerSecond={targetFormat.AverageBytesPerSecond} | " +
                $"Format={targetFormat}");
        }

        try
        {
            // Plain IEEE-float WaveFormat does not carry a channel mask. Keep the
            // application graph mono/stereo and let shared-mode WASAPI map it to
            // multichannel endpoint formats such as 5.1, 7.1, or spatial audio.
            int sampleChannels = targetFormat.Channels == 1 ? 1 : 2;
            int expectedBlockAlign = checked(
                sampleChannels * sizeof(float));
            int expectedAverageBytesPerSecond = checked(
                targetFormat.SampleRate * expectedBlockAlign);
            WaveFormat sampleFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                targetFormat.SampleRate,
                sampleChannels);
            if (sampleFormat.BlockAlign != expectedBlockAlign
                || sampleFormat.AverageBytesPerSecond
                    != expectedAverageBytesPerSecond)
            {
                throw new InvalidDataException(
                    $"生成的物理扬声器混音格式不完整。" +
                    $"BlockAlign={sampleFormat.BlockAlign} | " +
                    $"AverageBytesPerSecond={sampleFormat.AverageBytesPerSecond}");
            }
            return sampleFormat;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or ArgumentOutOfRangeException
            or InvalidDataException
            or OverflowException)
        {
            throw new NotSupportedException(
                $"无法为物理扬声器创建浮点混音格式。DeviceId={deviceID} | " +
                $"Format={targetFormat}",
                exception);
        }
    }

    private void StopPLEquipmentClear()
    {
        foreach (var item in PLEquipmentList.ToArray())
            item.Stop();
        PLEquipmentList.Clear();
    }

    private void StopPLEquipment()
    {
        StopPLEquipmentClear();
        var output = physicalLoudspeakerOutput;
        physicalLoudspeakerOutput = null;
        if (output != null)
            output.PlaybackStopped -= PhysicalLoudspeakerOutput_PlaybackStopped;
        DisposeOutputSafely(
            output,
            "停止或释放物理扬声器输出失败。",
            "AudioProxy.PhysicalLoudspeaker.Stop");
    }

    private static void DisposeOutputSafely(
        WasapiOut output,
        string message,
        string source)
    {
        if (output == null) return;
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            AppLog.Debug(
                $"停止 WASAPI 输出。Phase=Begin | State={output.PlaybackState}",
                source);
            output.Stop();
            AppLog.Debug(
                $"停止 WASAPI 输出。Phase=End | " +
                $"ElapsedMs={stopwatch.ElapsedMilliseconds}",
                source);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, message, source);
        }
        try
        {
            AppLog.Debug(
                $"释放 WASAPI 输出。Phase=Begin | " +
                $"ElapsedMs={stopwatch.ElapsedMilliseconds}",
                source);
            output.Dispose();
            AppLog.Debug(
                $"释放 WASAPI 输出。Phase=End | " +
                $"ElapsedMs={stopwatch.ElapsedMilliseconds}",
                source);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, message, source);
        }
    }

    private MixingSampleProvider audioMixToVL;
    private MixingSampleProvider audioMixToPL;
    private VolumeSampleProvider audioVolumeToVL;
    private VolumeSampleProvider audioVolumeToPL;

    private MixingSampleProvider physicalLoudspeakerMixer;
    private MixingSampleProvider virtualLoudspeakerMixer;

    //麦克风采集后方便通向虚拟扬声器和耳返
    private MicrophoneSampleBuffer[] physicalMicrophoneBufferArray = new MicrophoneSampleBuffer[2];
    private VolumeSampleProvider[] physicalMicrophoneVolumeArray = new VolumeSampleProvider[2];

    private VolumeSampleProvider physicalLoudspeakerVolume;
    private VolumeSampleProvider virtualLoudspeakerVolume;

    private MeteringSampleProvider audioToVLMetering;
    private MeteringSampleProvider audioToPLMetering;
    private MeteringSampleProvider physicalMicrophoneMetering;
    private MeteringSampleProvider virtualLoudspeakerMetering;
    private const int MeteringSampleProviderCollectionRate = 5;
    public void Start()
    {
        StopPLEquipmentClear();
        StopClear();
        var equipment = GlobalData.Inst.Equipment;

        // 初始化麦克风采集
        physicalMicrophoneCapture = new WasapiCapture(equipment.PhysicalMicrophone);
        physicalMicrophoneCapture.RecordingStopped += PhysicalMicrophoneCapture_RecordingStopped;
        WaveFormat captureFormat = physicalMicrophoneCapture.WaveFormat;
        var microphoneBuffers = new MicrophoneSampleBuffer[physicalMicrophoneBufferArray.Length];
        var microphoneVolumes = new VolumeSampleProvider[physicalMicrophoneVolumeArray.Length];
        for (int i = 0; i < microphoneBuffers.Length; i++)
        {
            microphoneBuffers[i] = new MicrophoneSampleBuffer(
                captureFormat,
                readFully: true,
                discardOnBufferOverflow: true,
                bufferDuration: TimeSpan.FromMilliseconds(500));
            microphoneVolumes[i] = new VolumeSampleProvider(
                microphoneBuffers[i].SampleProvider)
            {
                Volume = (float)AudioData.DecibelToVolume(microphoneInputDecibel)
            };
        }
        physicalMicrophoneBufferArray = microphoneBuffers;
        physicalMicrophoneVolumeArray = microphoneVolumes;
        physicalMicrophoneWaveFormat = microphoneBuffers[0].SampleFormat;
        AppLog.Information(
            $"麦克风采集格式已协商。" +
            $"DeviceId={equipment.PhysicalMicrophone.ID} | " +
            $"Capture={captureFormat} | Sample={physicalMicrophoneWaveFormat}",
            "AudioProxy.PhysicalMicrophone.Format");
        physicalMicrophoneCapture.DataAvailable += (sender, e) =>
        {
            if (GlobalData.Inst.AutoMute && AllAudioList.Count > 0)
                return;
            microphoneBuffers[0].AddSamples(e.Buffer, 0, e.BytesRecorded);

            if (GlobalData.Inst.GetGoEar_In())
                microphoneBuffers[1].AddSamples(e.Buffer, 0, e.BytesRecorded);
        };
        audioMixToVL = new MixingSampleProvider(physicalMicrophoneWaveFormat) { ReadFully = true };
        audioVolumeToVL = new VolumeSampleProvider(audioMixToVL);
        audioVolumeToVL.Volume = (float)AudioData.DecibelToVolume(audioOutDecibel);
        audioToVLMetering = new MeteringSampleProvider(audioVolumeToVL);
        SetStreamVolume(audioToVLMetering, SampleEnum.AutioToVL);

        virtualLoudspeakerMixer = new MixingSampleProvider(physicalMicrophoneWaveFormat) { ReadFully = true };
        physicalMicrophoneMetering = new MeteringSampleProvider(physicalMicrophoneVolumeArray[0]);
        SetStreamVolume(physicalMicrophoneMetering, SampleEnum.In);

        virtualLoudspeakerMixer.AddMixerInput(physicalMicrophoneMetering);
        virtualLoudspeakerMixer.AddMixerInput(audioToVLMetering);

        virtualLoudspeakerVolume = new VolumeSampleProvider(virtualLoudspeakerMixer);
        virtualLoudspeakerVolume.Volume = (float)AudioData.DecibelToVolume(globalDecibel);

        virtualLoudspeakerMetering = new MeteringSampleProvider(virtualLoudspeakerVolume);
        SetStreamVolume(virtualLoudspeakerMetering, SampleEnum.Out);



        virtualLoudspeakerOutput = new WasapiOut(equipment.VirtualLoudspeaker, AudioClientShareMode.Shared, false, 100);
        virtualLoudspeakerOutput.PlaybackStopped += VirtualLoudspeakerOutput_PlaybackStopped;

        virtualLoudspeakerOutput.Init(virtualLoudspeakerMetering);
        virtualLoudspeakerOutput.Play();
        physicalMicrophoneCapture.StartRecording();
    }

    private List<AudioData> AllAudioList = new();
    private List<AudioData> VLAudioList = new();
    private List<AudioData> PLEquipmentList = new();
    public void AddAudio(AudioData audioData)
    {
        if ((audioData.PlayMode & AudioData.PlayModeEnum.VL播放) == AudioData.PlayModeEnum.VL播放)
        {
            if (VLAudioList.Count == 0)
            {
                var beforePlayingKey = GlobalData.Inst.ActiveFunctionPreset?.BeforePlayingKey;
                switch (beforePlayingKey?.Action)
                {
                    case PlayAudioKeyData.KeyAction.按下:
                        HotkeyManager.Inst.SimulateHotkey(beforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        break;
                    case PlayAudioKeyData.KeyAction.抬起:
                        HotkeyManager.Inst.SimulateHotkey(beforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                    case PlayAudioKeyData.KeyAction.单击:
                        HotkeyManager.Inst.SimulateHotkey(beforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        HotkeyManager.Inst.SimulateHotkey(beforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                }
            }
            audioMixToVL.AddMixerInput(audioData.VolumeProvider_ToVM);
            VLAudioList.Add(audioData);
        }
        if ((audioData.PlayMode & AudioData.PlayModeEnum.PL播放) == AudioData.PlayModeEnum.PL播放)
        {
            audioMixToPL.AddMixerInput(audioData.VolumeProvider_ToPL);
            PLEquipmentList.Add(audioData);
        }

        AllAudioList.Add(audioData);
        while (AllAudioList.Count > MaxPlayingAudioCount)
            AllAudioList[0].Stop();
    }

    public void RemoveAudio(AudioData audioData)
    {
        AllAudioList.Remove(audioData);
        if ((audioData.PlayMode & AudioData.PlayModeEnum.VL播放) == AudioData.PlayModeEnum.VL播放)
        {
            audioMixToVL.RemoveMixerInput(audioData.VolumeProvider_ToVM);
            VLAudioList.Remove(audioData);
            if (VLAudioList.Count == 0)
            {
                var afterPlayingKey = GlobalData.Inst.ActiveFunctionPreset?.AfterPlayingKey;
                switch (afterPlayingKey?.Action)
                {
                    case PlayAudioKeyData.KeyAction.按下:
                        HotkeyManager.Inst.SimulateHotkey(afterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        break;
                    case PlayAudioKeyData.KeyAction.抬起:
                        HotkeyManager.Inst.SimulateHotkey(afterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                    case PlayAudioKeyData.KeyAction.单击:
                        HotkeyManager.Inst.SimulateHotkey(afterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        HotkeyManager.Inst.SimulateHotkey(afterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                }
            }
        }
        if ((audioData.PlayMode & AudioData.PlayModeEnum.PL播放) == AudioData.PlayModeEnum.PL播放)
        {
            audioMixToPL.RemoveMixerInput(audioData.VolumeProvider_ToPL);
            PLEquipmentList.Remove(audioData);
        }
    }

    public void StopClear()
    {
        foreach (var item in AllAudioList.ToArray())
            item.Stop();
        AllAudioList.Clear();
        VLAudioList.Clear();
        PLEquipmentList.Clear();
    }

    public void Stop()
    {
        StopClear();

        var capture = physicalMicrophoneCapture;
        physicalMicrophoneCapture = null;
        if (capture != null)
        {
            capture.RecordingStopped -= PhysicalMicrophoneCapture_RecordingStopped;
            try
            {
                capture.StopRecording();
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "停止物理麦克风采集失败。", "AudioProxy.Stop");
            }
            try
            {
                capture.Dispose();
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "释放物理麦克风采集失败。", "AudioProxy.Dispose");
            }
        }

        var output = virtualLoudspeakerOutput;
        virtualLoudspeakerOutput = null;
        if (output != null)
        {
            output.PlaybackStopped -= VirtualLoudspeakerOutput_PlaybackStopped;
            try
            {
                output.Stop();
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "停止虚拟扬声器输出失败。", "AudioProxy.Stop");
            }
            try
            {
                output.Dispose();
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "释放虚拟扬声器输出失败。", "AudioProxy.Dispose");
            }
        }

        Vol(MainWindow.Inst.IL, MainWindow.Inst.IR, 0, 0);
        Vol(MainWindow.Inst.OL, MainWindow.Inst.OR, 0, 0);
    }

    private void PhysicalLoudspeakerOutput_PlaybackStopped(
        object sender,
        StoppedEventArgs eventArgs)
    {
        Exception exception = eventArgs.Exception
            ?? new InvalidOperationException("物理扬声器输出意外停止。");
        DispatchRuntimeFailure(
            () => ReferenceEquals(sender, physicalLoudspeakerOutput),
            exception,
            "AudioProxy.PhysicalLoudspeaker.Runtime",
            () => GlobalData.Inst.Equipment.PhysicalLoudspeaker = null);
    }

    private void VirtualLoudspeakerOutput_PlaybackStopped(
        object sender,
        StoppedEventArgs eventArgs)
    {
        Exception exception = eventArgs.Exception
            ?? new InvalidOperationException("虚拟扬声器输出意外停止。");
        DispatchRuntimeFailure(
            () => ReferenceEquals(sender, virtualLoudspeakerOutput),
            exception,
            "AudioProxy.VirtualLoudspeaker.Runtime");
    }

    private void PhysicalMicrophoneCapture_RecordingStopped(
        object sender,
        StoppedEventArgs eventArgs)
    {
        Exception exception = eventArgs.Exception
            ?? new InvalidOperationException("物理麦克风采集意外停止。");
        DispatchRuntimeFailure(
            () => ReferenceEquals(sender, physicalMicrophoneCapture),
            exception,
            "AudioProxy.PhysicalMicrophone.Runtime");
    }

    private static void DispatchRuntimeFailure(
        Func<bool> isCurrentDevice,
        Exception exception,
        string source,
        Action afterFailure = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
        {
            AppLog.Error(
                exception,
                "音频设备在运行期间失效。",
                source);
            return;
        }

        try
        {
            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!isCurrentDevice()) return;
                    GlobalData.Inst.HandleAudioRuntimeFailure(exception, source);
                    afterFailure?.Invoke();
                }
                catch (Exception handlingException)
                {
                    AppLog.Error(
                        handlingException,
                        "处理运行期音频设备失效事件失败。",
                        source);
                }
            });
        }
        catch (Exception dispatchException)
        {
            AppLog.Error(
                dispatchException,
                "调度运行期音频设备失效事件失败。",
                source);
        }
    }


    public static void SetStreamVolume(MeteringSampleProvider meteringSample, SampleEnum index)
    {
        meteringSample.SamplesPerNotification = meteringSample.WaveFormat.SampleRate / MeteringSampleProviderCollectionRate;
        switch (index)
        {
            case SampleEnum.AutioToVL:
                meteringSample.StreamVolume += (sender, e) =>
                {
                    if (GlobalData.Inst.GetRun() == true)
                        Vol(e, MainWindow.Inst.AL, MainWindow.Inst.AR);
                };
                break;
            case SampleEnum.AutioToPL:
                meteringSample.StreamVolume += (sender, e) =>
                {
                    if (GlobalData.Inst.GetRun() == false)
                        Vol(e, MainWindow.Inst.AL, MainWindow.Inst.AR);
                };
                break;
            case SampleEnum.In:
                meteringSample.StreamVolume += (sender, e) =>
                {
                    Vol(e, MainWindow.Inst.IL, MainWindow.Inst.IR);
                };
                break;
            case SampleEnum.Out:
                meteringSample.StreamVolume += (sender, e) =>
                {
                    Vol(e, MainWindow.Inst.OL, MainWindow.Inst.OR);
                };
                break;
        }
    }

    private static void Vol(StreamVolumeEventArgs e, LevelBar L, LevelBar R)
    {
        if (e.MaxSampleValues.Length == 0)
        {
            Vol(L, R, 0, 0);
            return;
        }

        float leftChannelVol = e.MaxSampleValues[0];
        float rightChannelVol = e.MaxSampleValues.Length > 1 ? e.MaxSampleValues[1] : leftChannelVol;
        Vol(L, R, leftChannelVol, rightChannelVol);
    }
    public static void Vol(LevelBar L, LevelBar R, float leftChannelVol, float rightChannelVol)
    {
        float leftLevel = (float)AudioData.DecibelToProportion(
            AudioData.VolumeToDecibel(leftChannelVol));
        float rightLevel = (float)AudioData.DecibelToProportion(
            AudioData.VolumeToDecibel(rightChannelVol));
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
            return;

        void UpdateLevelBars()
        {
            L?.SetLevel(leftLevel);
            R?.SetLevel(rightLevel);
        }

        // Always enqueue the update. Stop paths enqueue their final zero after
        // the audio threads have ended, so older meter updates cannot overwrite it.
        try
        {
            dispatcher.BeginInvoke(new Action(UpdateLevelBars));
        }
        catch (InvalidOperationException) when (dispatcher.HasShutdownStarted)
        {
            // The application dispatcher completed shutdown after the guard above.
        }
    }

    public enum SampleEnum
    {
        AutioToVL,
        AutioToPL,
        In,
        Out,
    }
}
