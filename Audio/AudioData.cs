using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Diagnostics;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
#if AVALONIA
using Application = PlayVoice.DesktopApplication;
#else
using System.Windows;
#endif

namespace PlayVoice.Audio;

public class AudioData
{
    public AudioFileReader[] AudioTrackArray { get; set; } = new AudioFileReader[2];

    private VolumeSampleProvider virtualVolume, physicalVolume;
    private MediaFoundationResampler virtualResampler, physicalResampler;
    internal PlaybackOutput VirtualOutput { get; private set; }
    internal PlaybackOutput PhysicalOutput { get; private set; }
    internal bool IsPreview { get; private set; }
    private TrackPlaybackState state;
    private AudioEditSettings currentEdit;
    private TimeSpan sessionDuration, pausedPosition;
    public int Index { get; set; }
    public bool IsPlaying => state == TrackPlaybackState.Playing;
    public bool IsPaused => state == TrackPlaybackState.Paused;
    public event Action PlaybackChanged;

    private AudioEditSettings SavedEdit => AudioPreset == null ? null : Config.Edit;
    private double OriginalSeconds => AudioTrackArray[0]?.TotalTime.TotalSeconds ?? 0;
    public TimeSpan PlaybackDuration => state == TrackPlaybackState.Stopped
        ? TimeSpan.FromSeconds(SavedEdit?.Duration(OriginalSeconds) ?? OriginalSeconds)
        : sessionDuration;

    private TimeSpan Position
    {
        get
        {
            var position = (VirtualOutput ?? PhysicalOutput)?.Position ?? pausedPosition;
            return position < sessionDuration ? position : sessionDuration;
        }
    }

    public TrackPlaybackSnapshot Snapshot => new(
        AudioPreset == null ? System.IO.Path.GetFileName(AudioTrackArray[0]?.FileName) : Config.Name,
        state, (VirtualOutput == null ? PlaybackRoute.None : PlaybackRoute.Virtual)
            | (PhysicalOutput == null ? PlaybackRoute.None : PlaybackRoute.Physical),
        TimeSpan.FromSeconds((state == TrackPlaybackState.Stopped ? SavedEdit : currentEdit)?.StartSeconds ?? 0),
        PlaybackDuration, Position);

    public AudioPresetData AudioPreset { get; set; }

    public AudioDataConfig Config => AudioPreset.Config.AudioDataConfigList[Index];

    public void SetVolume(double decibel)
    {
        double normalizationDecibel = 0;
        if (AudioPreset != null
            && Config.Lufs.HasValue
            && GlobalData.Inst.Config.MicrophoneLufs.HasValue)
            normalizationDecibel = GlobalData.Inst.Config.MicrophoneLufs.Value - Config.Lufs.Value;

        var volume = decibel <= MinDecibel ? 0f : (float)DecibelToVolume(normalizationDecibel + decibel);
        if (virtualVolume != null)
        {
            virtualVolume.Volume = volume;
        }
        if (physicalVolume != null)
        {
            physicalVolume.Volume = volume;
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

    public void Start() => StartPlayback(preview: false, GlobalData.Inst.GetAudioDecibel(this));

    public void Start_创意工坊播放(double decibel = 0) => StartPlayback(preview: true, decibel);

    private void StartPlayback(bool preview, double decibel)
    {
        if (IsPlaying) { Stop(); return; }
        var global = GlobalData.Inst;
        bool toVirtual = !preview && global.GetRun();
        bool toPhysical = toVirtual ? global.GetGoEar_Audio() : global.Equipment.PhysicalLoudspeakerState;
        if (!toVirtual && !toPhysical)
        {
            MainWindow.Inst.AddNotification(() => LanguageManager.Inst.GetString("通知"),
                () => LanguageManager.Inst.GetString("物理扬声器") + " " + LanguageManager.Inst.GetString("未绑定"),
                Pages.LabelStatus.Warning, 4);
            return;
        }

        bool resume = IsPaused;
        if (!resume)
        {
            var edit = SavedEdit ?? new AudioEditSettings();
            edit.Validate(OriginalSeconds, AudioTrackArray[0].WaveFormat.SampleRate);
            currentEdit = new AudioEditSettings
            {
                StartSeconds = edit.StartSeconds, EndSeconds = edit.EndSeconds,
                FadeInSeconds = edit.FadeInSeconds, FadeOutSeconds = edit.FadeOutSeconds
            };
            sessionDuration = TimeSpan.FromSeconds(currentEdit.Duration(OriginalSeconds));
        }
        IsPreview = preview;
        var proxy = global.AudioProxy;
        try
        {
            if (toVirtual)
                VirtualOutput = CreateOutput(0, proxy.PhysicalMicrophoneWaveFormat, pausedPosition, out virtualResampler, out virtualVolume);
            if (toPhysical)
                PhysicalOutput = CreateOutput(1, proxy.PhysicalLoudspeakerWaveFormat, pausedPosition, out physicalResampler, out physicalVolume);
            SetVolume(decibel);
            state = TrackPlaybackState.Playing;
            proxy.AddAudio(this);
            if (!preview && !resume)
            {
                Config.LastPlayedAt = DateTimeOffset.UtcNow;
                AudioPreset.PlayHistoryVersion++;
                AudioPreset.Save();
                if (!toVirtual && global.Config.MicrophoneLufs == null)
                    MainWindow.Inst.AddNotification(() => LanguageManager.Inst.GetString("通知"),
                        () => LanguageManager.Inst.GetString("未完成响度测试，音频响度可能过高"), Pages.LabelStatus.Warning, 4);
            }
        }
        catch
        {
            proxy.RemoveAudio(this);
            DisposeOutputs();
            state = resume ? TrackPlaybackState.Paused : TrackPlaybackState.Stopped;
            throw;
        }
        PlaybackChanged?.Invoke();
        if (preview) global.Playback.NotifyChanged();
    }

    private PlaybackOutput CreateOutput(int index, WaveFormat format, TimeSpan start,
        out MediaFoundationResampler resampler, out VolumeSampleProvider volume)
    {
        var edited = new EditedSampleProvider(AudioTrackArray[index], currentEdit, start.TotalSeconds);
        resampler = new MediaFoundationResampler(edited.ToWaveProvider(), format) { ResamplerQuality = 60 };
        volume = new VolumeSampleProvider(resampler.ToSampleProvider());
        return new PlaybackOutput(volume, start, OutputEnded);
    }

    private void OutputEnded(PlaybackOutput ended)
    {
        // A mixer holds its input lock during Read. Post cleanup and validate the
        // output identity so a late EOF cannot stop a resumed or replayed track.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(() =>
        {
            if (ended == VirtualOutput || ended == PhysicalOutput) CompleteIfDrained();
        });
    }

    private void CompleteIfDrained()
    {
        if (IsPlaying && VirtualOutput?.IsEnded != false && PhysicalOutput?.IsEnded != false)
            StopCore(completed: true);
    }

    internal void SetMonitoring(bool enabled)
    {
        if (VirtualOutput == null || enabled == (PhysicalOutput != null)) return;
        var proxy = GlobalData.Inst.AudioProxy;
        if (enabled)
        {
            if (VirtualOutput.IsEnded) return;
            try
            {
                PhysicalOutput = CreateOutput(1, proxy.PhysicalLoudspeakerWaveFormat, Position,
                    out physicalResampler, out physicalVolume);
                physicalVolume.Volume = virtualVolume.Volume;
                proxy.AddPhysicalInput(this);
            }
            catch
            {
                ReleasePhysicalOutput();
                throw;
            }
        }
        else
        {
            proxy.RemovePhysicalInput(this);
            ReleasePhysicalOutput();
            CompleteIfDrained();
        }
        PlaybackChanged?.Invoke();
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        GlobalData.Inst.AudioProxy.RemoveAudio(this);
        pausedPosition = Position;
        DisposeOutputs();
        state = TrackPlaybackState.Paused;
        PlaybackChanged?.Invoke();
    }

    public void Stop() => StopCore(completed: false);

    private void StopCore(bool completed)
    {
        if (state == TrackPlaybackState.Stopped) return;
        if (IsPlaying) GlobalData.Inst.AudioProxy.RemoveAudio(this);
        DisposeOutputs();
        pausedPosition = TimeSpan.Zero;
        currentEdit = null;
        state = TrackPlaybackState.Stopped;
        PlaybackChanged?.Invoke();
        if (completed && !IsPreview) GlobalData.Inst.Playback.OnCompleted(this);
        else GlobalData.Inst.Playback.NotifyChanged();
    }

    private void ReleasePhysicalOutput()
    {
        PhysicalOutput = null;
        physicalVolume = null;
        physicalResampler?.Dispose();
        physicalResampler = null;
    }

    private void DisposeOutputs()
    {
        ReleasePhysicalOutput();
        VirtualOutput = null;
        virtualVolume = null;
        virtualResampler?.Dispose();
        virtualResampler = null;
    }

    public void Dispose()
    {
        Stop();
        foreach (var reader in AudioTrackArray) reader?.Dispose();
        PlaybackChanged = null;
    }

    public static string DurationToString(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{(long)duration.TotalMinutes}m {duration.Seconds % 60}s";
        }
        else if (duration.Seconds > 0)
        {
            return $"{duration.Seconds}.{duration.Milliseconds:000}s";
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

