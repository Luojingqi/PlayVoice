using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Diagnostics;
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

    public bool IsPlaying =>
        (PlayMode & PlayModeEnum.不播放) != PlayModeEnum.不播放;

    public double PlaybackProgress
    {
        get
        {
            if (!IsPlaying && !IsPaused)
                return 0;

            double duration = AudioTrackArray[0]?.TotalTime.TotalMilliseconds ?? 0;
            return duration > 0
                ? Math.Clamp(playbackClock.Elapsed.TotalMilliseconds / duration, 0, 1)
                : 0;
        }
    }

    public int Index { get; set; }

    public System.Windows.Threading.DispatcherTimer PlayTimer { get; } = new(System.Windows.Threading.DispatcherPriority.Normal, Application.Current.Dispatcher);
    public bool IsPaused { get; private set; }
    private MediaFoundationResampler vmResampler;
    private MediaFoundationResampler plResampler;
    private bool workshopPlayback;
    // Reader.Position includes decoder/resampler read-ahead, not audible progress.
    private readonly Stopwatch playbackClock = new();
    private PlayModeEnum pausedPlayMode;

    public AudioPresetData AudioPreset { get; set; }

    public AudioDataConfig Config => AudioPreset.Config.AudioDataConfigList[Index];

    public void SetVolume(double decibel)
    {
        double normalizationDecibel = 0;
        if (AudioPreset != null
            && Config.Lufs.HasValue
            && GlobalData.Inst.Config.MicrophoneLufs.HasValue)
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
        PlayTimer.Tick += (_, _) => StopCore(completed: true);
    }

    public Action AnimationPlayAction;
    public Action AnimationStopAction;
    public event Action PlaybackStarted;
    public event Action PlaybackStopped;


    public void Start()
    {
        if ((PlayMode & PlayModeEnum.不播放) != PlayModeEnum.不播放) Stop();
        else
        {
            if (IsPaused && TryResume()) return;
            workshopPlayback = false;
            var audioProxy = GlobalData.Inst.AudioProxy;
            bool canPlay = false;
            if (GlobalData.Inst.GetRun())
            {
                PlayMode = 0;
                PlayMode |= PlayModeEnum.VL播放;
                VolumeProvider_ToVM = new VolumeSampleProvider(
                  (vmResampler = new MediaFoundationResampler(AudioTrackArray[0], audioProxy.PhysicalMicrophoneWaveFormat) { ResamplerQuality = 60 }).ToSampleProvider());
                if (GlobalData.Inst.GetGoEar_Audio())
                {
                    VolumeProvider_ToPL = new VolumeSampleProvider(
                      (plResampler = new MediaFoundationResampler(AudioTrackArray[1], audioProxy.PhysicalLoudspeakerWaveFormat) { ResamplerQuality = 60 }).ToSampleProvider());
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
                  (plResampler = new MediaFoundationResampler(AudioTrackArray[1], audioProxy.PhysicalLoudspeakerWaveFormat) { ResamplerQuality = 60 }).ToSampleProvider());
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
            Config.LastPlayedAt = DateTimeOffset.UtcNow;
            AudioPreset.Save();
            IsPaused = false;
            PlayTimer.Interval = RemainingTime;
            SetVolume(GlobalData.Inst.GetAudioDecibel(this));
            audioProxy.AddAudio(this);
            playbackClock.Start();
            PlayTimer.Start();
            AnimationPlayAction?.Invoke();
            PlaybackStarted?.Invoke();
        }
    }

    /// <summary>
    /// 由创意工坊本地预览播放
    /// </summary>
    public void Start_创意工坊播放()
    {
        workshopPlayback = true;
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
                  (plResampler = new MediaFoundationResampler(AudioTrackArray[1], audioProxy.PhysicalLoudspeakerWaveFormat) { ResamplerQuality = 60 }).ToSampleProvider());
                IsPaused = false;
                PlayTimer.Interval = RemainingTime;
                SetVolume(0);
                audioProxy.AddAudio(this);
                playbackClock.Start();
                PlayTimer.Start();
                AnimationPlayAction?.Invoke();
                PlaybackStarted?.Invoke();
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

    private TimeSpan RemainingTime => TimeSpan.FromMilliseconds(Math.Max(1,
        AudioTrackArray[0].TotalTime.TotalMilliseconds - playbackClock.Elapsed.TotalMilliseconds));

    private bool TryResume()
    {
        var global = GlobalData.Inst;
        var proxy = global.AudioProxy;
        var desired = global.GetRun()
            ? PlayModeEnum.VL播放 | (global.GetGoEar_Audio() ? PlayModeEnum.PL播放 : 0)
            : global.Equipment.PhysicalLoudspeakerState ? PlayModeEnum.PL播放 : PlayModeEnum.不播放;
        if (desired == pausedPlayMode
            && ((desired & PlayModeEnum.VL播放) == 0 || vmResampler?.WaveFormat.Equals(proxy.PhysicalMicrophoneWaveFormat) == true)
            && ((desired & PlayModeEnum.PL播放) == 0 || plResampler?.WaveFormat.Equals(proxy.PhysicalLoudspeakerWaveFormat) == true))
        {
            // Keep decoder buffers intact: rebuilding from the reader's prefetched position skips audio.
            PlayMode = pausedPlayMode;
            IsPaused = false;
            SetVolume(global.GetAudioDecibel(this));
            proxy.AddAudio(this);
            PlayTimer.Interval = RemainingTime;
            playbackClock.Start();
            PlayTimer.Start();
            AnimationPlayAction?.Invoke();
            PlaybackStarted?.Invoke();
            return true;
        }

        // The output route changed while paused. Rebuild at the frozen playback position.
        DisposeProviders();
        foreach (var track in AudioTrackArray)
            track.CurrentTime = TimeSpan.FromMilliseconds(Math.Min(
                playbackClock.Elapsed.TotalMilliseconds, track.TotalTime.TotalMilliseconds));
        return false;
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        PlayTimer.Stop();
        playbackClock.Stop();
        pausedPlayMode = PlayMode;
        GlobalData.Inst.AudioProxy.RemoveAudio(this);
        IsPaused = true;
        PlayMode = PlayModeEnum.不播放;
        AnimationStopAction?.Invoke();
        PlaybackStopped?.Invoke();
    }

    public void Stop() => StopCore(completed: false);

    private void StopCore(bool completed)
    {
        if (!IsPlaying && !IsPaused) return;
        PlayTimer.Stop();
        playbackClock.Reset();
        if (IsPlaying)
            GlobalData.Inst.AudioProxy.RemoveAudio(this);
        DisposeProviders();
        foreach (var track in AudioTrackArray)
            if (track != null) track.Position = 0;
        IsPaused = false;
        PlayMode = PlayModeEnum.不播放;
        AnimationStopAction?.Invoke();
        PlaybackStopped?.Invoke();
        if (!workshopPlayback)
        {
            if (completed) GlobalData.Inst.Playback.OnCompleted(this);
            else GlobalData.Inst.Playback.OnStopped(this);
        }
    }

    private void DisposeProviders()
    {
        VolumeProvider_ToVM = null;
        VolumeProvider_ToPL = null;
        vmResampler?.Dispose();
        plResampler?.Dispose();
        vmResampler = null;
        plResampler = null;
    }

    public void Stop_创意工坊播放() => Stop();

    public void Dispose()
    {
        Stop();
        for (int i = 0; i < AudioTrackArray.Length; i++)
            AudioTrackArray[i]?.Dispose();
        AnimationPlayAction = null;
        AnimationStopAction = null;
        PlaybackStarted = null;
        PlaybackStopped = null;
        VolumeProvider_ToVM = null;
        VolumeProvider_ToPL = null;
        PlayTimer.Stop();

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

    public static async Task<double?> MeasureLufs(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process
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
        Task<string> outputTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException exception)
            {
                AppLog.Warning(
                    exception,
                    "取消响度检测时 FFmpeg 进程状态已改变。",
                    "AudioData.Loudness");
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                AppLog.Warning(
                    exception,
                    "取消响度检测时无法终止 FFmpeg。",
                    "AudioData.Loudness");
            }

            try
            {
                await process.WaitForExitAsync().WaitAsync(
                    TimeSpan.FromSeconds(5));
                await outputTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception exception)
            {
                AppLog.Warning(
                    exception,
                    "取消响度检测时等待 FFmpeg 退出或读取输出失败。",
                    "AudioData.Loudness");
            }

            throw;
        }

        string output = await outputTask;
        cancellationToken.ThrowIfCancellationRequested();

        var match = Regex.Match(output, @"I:\s+([-0-9.]+)\s+LUFS", RegexOptions.RightToLeft);

        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lufs))
        {
            return lufs;
        }
        AppLog.Warning(
            $"未能解析 {filePath} 的 LUFS。FFmpeg 输出：{output}",
            "AudioData.Loudness");
        return null;
    }
}

