using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace PlayVoice.Audio;

public class AudioData
{
    public AudioFileReader[] AudioTrackArray { get; set; } = new AudioFileReader[2];

    public VolumeSampleProvider VolumeProvider_ToVM { get; set; }
    public VolumeSampleProvider VolumeProvider_ToPL { get; set; }

    public PlayModeEnum PlayMode { get; set; } = PlayModeEnum.不播放;

    public int Index { get; set; }

    public System.Timers.Timer PlayTimer { get; set; } = new System.Timers.Timer();

    public PresetData Preset { get; set; }

    public AudioDataConfig Config => Preset.Config.AudioDataConfigList[Index];

    public void SetVolume(double decibel)
    {
        double normalizationDecibel = 0;
        if (Config.Lufs.HasValue && GlobalData.Inst.Config.MicrophoneLufs.HasValue)
            normalizationDecibel = GlobalData.Inst.Config.MicrophoneLufs.Value - Config.Lufs.Value;

        var volume = (float)DecibelToVolume(normalizationDecibel + decibel);
        if (VolumeProvider_ToVM != null)
        {
            VolumeProvider_ToVM.Volume = volume;
        }
        if (VolumeProvider_ToPL != null)
        {
            VolumeProvider_ToPL.Volume = volume;
        }
    }

    public static readonly double MinDecibel = -90.0;
    public static readonly double MaxDecibel = 6.0;

    public static double DecibelToVolume(double decibel)
    {
        if (decibel <= MinDecibel) return 0;
        return Math.Pow(10.0, decibel / 20.0);
    }

    public static double VolumeToDecibel(double volume)
    {
        if (volume <= 0) return MinDecibel;
        return Math.Clamp(20.0 * Math.Log10(volume), MinDecibel, MaxDecibel);
    }

    public static double ProportionToDecibel(double proportion)
    {
        if (proportion <= 1)
            return proportion * Math.Abs(MinDecibel) + MinDecibel;
        else
            return (proportion - 1) * MaxDecibel;
    }

    public static double DecibelToProportion(double decibel)
    {
        var absMinDecibel = Math.Abs(MinDecibel);
        if (decibel <= 0)
            return (decibel + absMinDecibel) / absMinDecibel;
        else
            return decibel / MaxDecibel + 1;
    }

    public AudioData()
    {
        PlayTimer.Elapsed += (o, e) => Application.Current.Dispatcher.Invoke(Stop);
    }

    public Action AnimationPlayAction;
    public Action AnimationStopAction;


    public void Start()
    {
        if ((PlayMode & PlayModeEnum.不播放) != PlayModeEnum.不播放) Stop();
        else
        {
            var audioProxy = GlobalData.Inst.AudioProxy;
            bool canPlay = false;
            if (GlobalData.Inst.GetRun())
            {
                PlayMode = 0;
                PlayMode |= PlayModeEnum.VL播放;
                VolumeProvider_ToVM = new VolumeSampleProvider(
                  new MediaFoundationResampler(AudioTrackArray[0], audioProxy.PhysicalMicrophoneWaveFormat) { ResamplerQuality = 60 }.ToSampleProvider());
                if (GlobalData.Inst.GetGoEar_Audio())
                {
                    VolumeProvider_ToPL = new VolumeSampleProvider(
                      new MediaFoundationResampler(AudioTrackArray[1], audioProxy.PhysicalLoudspeakerWaveFormat) { ResamplerQuality = 60 }.ToSampleProvider());
                    PlayMode |= PlayModeEnum.PL播放;
                }
                canPlay = true;
            }
            else if (GlobalData.Inst.Equipment.PhysicalLoudspeakerState)
            {
                PlayMode = 0;
                PlayMode |= PlayModeEnum.PL播放;
                VolumeProvider_ToVM = null;
                VolumeProvider_ToPL = new VolumeSampleProvider(
                  new MediaFoundationResampler(AudioTrackArray[1], audioProxy.PhysicalLoudspeakerWaveFormat) { ResamplerQuality = 60 }.ToSampleProvider());
                canPlay = true;
                if (GlobalData.Inst.Config.MicrophoneLufs == null)
                {
                    MainWindow.Inst.AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("未完成响度测试，音频响度可能过高")}",
                        Pages.LabelStatus.Warning, 4);
                }
            }
            else
            {
                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("物理扬声器")} {LanguageManager.Inst.GetString("未绑定")}",
                    Pages.LabelStatus.Warning, 4);
            }

            if (canPlay == false) return;
            PlayTimer.Interval = AudioTrackArray[0].TotalTime.TotalMilliseconds;
            SetVolume(Config.Decibel);
            audioProxy.AddAudio(this);
            PlayTimer.Start();
            AnimationPlayAction?.Invoke();
        }
    }

    /// <summary>
    /// 由创意工坊本地预览播放
    /// </summary>
    public void Start_创意工坊播放()
    {
        if ((PlayMode & PlayModeEnum.不播放) != PlayModeEnum.不播放) Stop();
        else
        {
            var audioProxy = GlobalData.Inst.AudioProxy;
            if (GlobalData.Inst.Equipment.PhysicalLoudspeakerState)
            {
                PlayMode = 0;
                PlayMode |= PlayModeEnum.PL播放;
                VolumeProvider_ToVM = null;
                VolumeProvider_ToPL = new VolumeSampleProvider(
                  new MediaFoundationResampler(AudioTrackArray[1], audioProxy.PhysicalLoudspeakerWaveFormat) { ResamplerQuality = 60 }.ToSampleProvider());
                PlayTimer.Interval = AudioTrackArray[0].TotalTime.TotalMilliseconds;
                SetVolume(Config.Decibel);
                audioProxy.AddAudio(this);
                PlayTimer.Start();
                AnimationPlayAction?.Invoke();
            }
            else
            {
                MainWindow.Inst.AddNotification(
                       () => $"{LanguageManager.Inst.GetString("通知")}",
                       () => $"{LanguageManager.Inst.GetString("软件名称")} {LanguageManager.Inst.GetString("未绑定声卡")}",
                       Pages.LabelStatus.Warning, 4);
            }
        }
    }

    public void Stop()
    {
        if ((PlayMode & PlayModeEnum.不播放) == PlayModeEnum.不播放) return;

        PlayTimer.Stop();
        var audioProxy = GlobalData.Inst.AudioProxy;
        audioProxy.RemoveAudio(this);
        for (int i = 0; i < AudioTrackArray.Length; i++)
            AudioTrackArray[i].Position = 0;
        VolumeProvider_ToVM = null;
        VolumeProvider_ToPL = null;
        PlayMode = PlayModeEnum.不播放;
        AnimationStopAction?.Invoke();
    }


    public void Stop_创意工坊播放()
    {
        if ((PlayMode & PlayModeEnum.不播放) == PlayModeEnum.不播放) return;
        PlayTimer.Stop();
        var audioProxy = GlobalData.Inst.AudioProxy;
        audioProxy.RemoveAudio(this);
        for (int i = 0; i < AudioTrackArray.Length; i++)
            AudioTrackArray[i].Position = 0;
        VolumeProvider_ToVM = null;
        VolumeProvider_ToPL = null;
        PlayMode = PlayModeEnum.不播放;
        AnimationStopAction?.Invoke();
    }

    public void Dispose()
    {
        Stop();
        if (Preset != null)
            Config.HotkeyData.Callback = null;
        for (int i = 0; i < AudioTrackArray.Length; i++)
            AudioTrackArray[i].Dispose();
        AnimationPlayAction = null;
        AnimationStopAction = null;
        VolumeProvider_ToVM = null;
        VolumeProvider_ToPL = null;
        PlayTimer.Stop();
        PlayTimer.Dispose();
    }

    public enum PlayModeEnum
    {
        不播放 = 1 << 0,
        PL播放 = 1 << 1,
        VL播放 = 1 << 2,
    }


    public static string DurationToString(TimeSpan duration)
    {
        if (duration.Minutes > 0)
        {
            return $"{duration.Minutes}m {duration.Seconds % 60}s";
        }
        else if (duration.Seconds > 0)
        {
            return $"{duration.Seconds}.{duration.Milliseconds % 1000}s";
        }
        else
        {
            return $"{duration.Milliseconds}ms";
        }
    }

    public static string SizeToString(long size)
    {
        if (size >= 1024 * 1024 * 1024)
        {
            return $"{(size / (1024.0 * 1024.0 * 1024.0)):F2} GB";
        }
        else if (size >= 1024 * 1024)
        {
            return $"{(size / (1024.0 * 1024.0)):F2} MB";
        }
        else if (size >= 1024)
        {
            return $"{(size / 1024.0):F2} KB";
        }
        else
        {
            return $"{size} B";
        }
    }

    public static async Task<double?> MeasureLufs(string filePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -i \"{filePath}\" -filter:a ebur128 -f null -",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var match = Regex.Match(output, @"I:\s+([-0-9.]+)\s+LUFS", RegexOptions.RightToLeft);

        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lufs))
        {
            return lufs;
        }
        Console.WriteLine($"[警告] 未能解析 {filePath} 的 LUFS。FFmpeg 输出:\n{output}");
        return null;
    }
}

