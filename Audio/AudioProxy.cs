using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Hotkey;
using PlayVoice.Pages.LevelBar;
using PlayVoice.Pages.Workshop;
using System.Windows;

namespace PlayVoice.Audio;

/// <summary>
/// 音频代理器
/// </summary>
internal class AudioProxy
{
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

    public AudioProxy()
    {
        Inst = this;
    }

    public void Init()
    {
        GlobalData.Inst.Equipment.PhysicalLoudspeakerOnChanged += (device0, device1) =>
        {
            if (device0 != null) StopPLEquipment();
            if (device1 != null) StartPLEquipment();
        };
        GlobalData.Inst.GoEar_InStateChanged += (value) =>
        {
            if (value)
            {
                physicalLoudspeakerMixer.AddMixerInput(physicalMicrophoneVolumeArray[1]);
                StopPLEquipmentClear();
            }
            else
            {
                physicalLoudspeakerMixer.RemoveMixerInput(physicalMicrophoneVolumeArray[1]);
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
    }


    private void StartPLEquipment()
    {
        MMDevice device = GlobalData.Inst.Equipment.PhysicalLoudspeaker;
        WaveFormat targetFormat = device.AudioClient.MixFormat;
        physicalLoudspeakerWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetFormat.SampleRate, targetFormat.Channels);
        physicalLoudspeakerMixer = new MixingSampleProvider(physicalLoudspeakerWaveFormat) { ReadFully = true };
        physicalLoudspeakerOutput = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
        physicalLoudspeakerVolume = new VolumeSampleProvider(physicalLoudspeakerMixer);
        physicalLoudspeakerVolume.Volume = (float)AudioData.DecibelToVolume(globalDecibel);
        audioMixToPL = new MixingSampleProvider(physicalLoudspeakerWaveFormat) { ReadFully = true };
        audioVolumeToPL = new VolumeSampleProvider(audioMixToPL);
        audioVolumeToPL.Volume = (float)AudioData.DecibelToVolume(audioOutDecibel);
        audioToPLMetering = new MeteringSampleProvider(audioVolumeToPL);
        SetStreamVolume(audioToPLMetering, SampleEnum.AutioToPL);
        physicalLoudspeakerMixer.AddMixerInput(audioToPLMetering);

        physicalLoudspeakerOutput.Init(physicalLoudspeakerVolume);
        physicalLoudspeakerOutput.Play();
    }

    private void StopPLEquipmentClear()
    {
        foreach (var item in PLEquipmentSet)
            item.Stop();
        PLEquipmentSet.Clear();
    }

    private void StopPLEquipment()
    {
        StopPLEquipmentClear();
        physicalLoudspeakerOutput?.Stop();
    }

    private MixingSampleProvider audioMixToVL;
    private MixingSampleProvider audioMixToPL;
    private VolumeSampleProvider audioVolumeToVL;
    private VolumeSampleProvider audioVolumeToPL;

    private MixingSampleProvider physicalLoudspeakerMixer;
    private MixingSampleProvider virtualLoudspeakerMixer;

    //麦克风采集后方便通向虚拟扬声器和耳返
    private BufferedWaveProvider[] physicalMicrophoneBufferArray = new BufferedWaveProvider[2];
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
        WaveFormat targetFormat = equipment.PhysicalMicrophone.AudioClient.MixFormat;
        physicalMicrophoneCapture = new WasapiCapture(equipment.PhysicalMicrophone);
        physicalMicrophoneWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetFormat.SampleRate, targetFormat.Channels);
        physicalMicrophoneCapture.WaveFormat = physicalMicrophoneWaveFormat;
        physicalMicrophoneCapture.DataAvailable += (sender, e) =>
        {
            if (GlobalData.Inst.AutoMute && AllAudioSet.Count > 0)
                return;
            physicalMicrophoneBufferArray[0].AddSamples(e.Buffer, 0, e.BytesRecorded);

            if (GlobalData.Inst.GetGoEar_In())
                physicalMicrophoneBufferArray[1].AddSamples(e.Buffer, 0, e.BytesRecorded);
        };
        physicalMicrophoneCapture.StartRecording();

        audioMixToVL = new MixingSampleProvider(physicalMicrophoneWaveFormat) { ReadFully = true };
        audioVolumeToVL = new VolumeSampleProvider(audioMixToVL);
        audioVolumeToVL.Volume = (float)AudioData.DecibelToVolume(audioOutDecibel);
        audioToVLMetering = new MeteringSampleProvider(audioVolumeToVL);
        SetStreamVolume(audioToVLMetering, SampleEnum.AutioToVL);

        virtualLoudspeakerMixer = new MixingSampleProvider(physicalMicrophoneWaveFormat) { ReadFully = true };
        for (int i = 0; i < physicalMicrophoneBufferArray.Length; i++)
        {
            physicalMicrophoneBufferArray[i] = new BufferedWaveProvider(physicalMicrophoneWaveFormat)
            { DiscardOnBufferOverflow = true };

            physicalMicrophoneVolumeArray[i] = new VolumeSampleProvider(physicalMicrophoneBufferArray[i].ToSampleProvider());
            physicalMicrophoneVolumeArray[i].Volume = (float)AudioData.DecibelToVolume(microphoneInputDecibel);
        }

        physicalMicrophoneMetering = new MeteringSampleProvider(physicalMicrophoneVolumeArray[0]);
        SetStreamVolume(physicalMicrophoneMetering, SampleEnum.In);

        virtualLoudspeakerMixer.AddMixerInput(physicalMicrophoneMetering);
        virtualLoudspeakerMixer.AddMixerInput(audioToVLMetering);

        virtualLoudspeakerVolume = new VolumeSampleProvider(virtualLoudspeakerMixer);
        virtualLoudspeakerVolume.Volume = (float)AudioData.DecibelToVolume(globalDecibel);

        virtualLoudspeakerMetering = new MeteringSampleProvider(virtualLoudspeakerVolume);
        SetStreamVolume(virtualLoudspeakerMetering, SampleEnum.Out);



        virtualLoudspeakerOutput = new WasapiOut(equipment.VirtualLoudspeaker, AudioClientShareMode.Shared, false, 100);

        virtualLoudspeakerOutput.Init(virtualLoudspeakerMetering);
        virtualLoudspeakerOutput.Play();
    }

    private HashSet<AudioData> AllAudioSet = new();
    private HashSet<AudioData> VLAudioSet = new();
    private HashSet<AudioData> PLEquipmentSet = new();
    public void AddAudio(AudioData audioData)
    {
        if ((audioData.PlayMode & AudioData.PlayModeEnum.VL播放) == AudioData.PlayModeEnum.VL播放)
        {
            if (VLAudioSet.Count == 0)
            {
                var config = GlobalData.Inst.Config;
                switch (config.BeforePlayingKey.Action)
                {
                    case PlayAudioKeyData.KeyAction.按下:
                        HotkeyManager.Inst.SimulateHotkey(config.BeforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        break;
                    case PlayAudioKeyData.KeyAction.抬起:
                        HotkeyManager.Inst.SimulateHotkey(config.BeforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                    case PlayAudioKeyData.KeyAction.单击:
                        HotkeyManager.Inst.SimulateHotkey(config.BeforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        HotkeyManager.Inst.SimulateHotkey(config.BeforePlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                }
            }
            audioMixToVL.AddMixerInput(audioData.VolumeProvider_ToVM);
            VLAudioSet.Add(audioData);
        }
        if ((audioData.PlayMode & AudioData.PlayModeEnum.PL播放) == AudioData.PlayModeEnum.PL播放)
        {
            audioMixToPL.AddMixerInput(audioData.VolumeProvider_ToPL);
            PLEquipmentSet.Add(audioData);
        }

        AllAudioSet.Add(audioData);
    }

    public void RemoveAudio(AudioData audioData)
    {
        AllAudioSet.Remove(audioData);
        if ((audioData.PlayMode & AudioData.PlayModeEnum.VL播放) == AudioData.PlayModeEnum.VL播放)
        {
            audioMixToVL.RemoveMixerInput(audioData.VolumeProvider_ToVM);
            VLAudioSet.Remove(audioData);
            if (VLAudioSet.Count == 0)
            {
                var config = GlobalData.Inst.Config;
                switch (config.AfterPlayingKey.Action)
                {
                    case PlayAudioKeyData.KeyAction.按下:
                        HotkeyManager.Inst.SimulateHotkey(config.AfterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        break;
                    case PlayAudioKeyData.KeyAction.抬起:
                        HotkeyManager.Inst.SimulateHotkey(config.AfterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                    case PlayAudioKeyData.KeyAction.单击:
                        HotkeyManager.Inst.SimulateHotkey(config.AfterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Down);
                        HotkeyManager.Inst.SimulateHotkey(config.AfterPlayingKey.HotkeyData, HotkeyManager.KeyAction.Up);
                        break;
                }
            }
        }
        if ((audioData.PlayMode & AudioData.PlayModeEnum.PL播放) == AudioData.PlayModeEnum.PL播放)
        {
            audioMixToPL.RemoveMixerInput(audioData.VolumeProvider_ToPL);
            PLEquipmentSet.Remove(audioData);
        }
    }

    public void StopClear()
    {
        foreach (var item in AllAudioSet)
            item.Stop();
        AllAudioSet.Clear();
        VLAudioSet.Clear();
        PLEquipmentSet.Clear();
    }

    public void Stop()
    {
        StopClear();
        
        physicalMicrophoneCapture?.StopRecording();
        virtualLoudspeakerOutput?.Stop();

        physicalMicrophoneCapture?.Dispose();
        virtualLoudspeakerOutput?.Dispose();

        Vol(MainWindow.Inst.IL, MainWindow.Inst.IR, 0, 0);
        Vol(MainWindow.Inst.OL, MainWindow.Inst.OR, 0, 0);
    }


    public static void SetStreamVolume(MeteringSampleProvider meteringSample, SampleEnum index)
    {
        meteringSample.SamplesPerNotification = meteringSample.WaveFormat.SampleRate / MeteringSampleProviderCollectionRate;
        LevelBar L = null;
        LevelBar R = null;
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
        float leftChannelVol = e.MaxSampleValues[0];
        float rightChannelVol = e.MaxSampleValues.Length > 1 ? e.MaxSampleValues[1] : leftChannelVol;
        Vol(L, R, leftChannelVol, rightChannelVol);
    }
    public static void Vol(LevelBar L, LevelBar R, float leftChannelVol, float rightChannelVol)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            L?.SetLevel((float)AudioData.DecibelToProportion(AudioData.VolumeToDecibel(leftChannelVol)));
            R?.SetLevel((float)AudioData.DecibelToProportion(AudioData.VolumeToDecibel(rightChannelVol)));
        });
    }

    public enum SampleEnum
    {
        AutioToVL,
        AutioToPL,
        In,
        Out,
    }
}