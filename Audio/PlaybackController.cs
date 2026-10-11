using PlayVoice.Diagnostics;

namespace PlayVoice.Audio;

public enum PlaybackMode { Single, RepeatOne, RepeatList, Shuffle }

/// <summary>UI-thread playback state. Queue and position are never serialized.</summary>
public sealed class PlaybackController
{
    private readonly PlaybackQueue<AudioData> queue = new();
    public AudioData Current => queue.Current;
    public event Action Changed;
    private IReadOnlyList<AudioData> Tracks => GlobalData.Inst.ActiveAudioPreset?.AudioList
        ?? (IReadOnlyList<AudioData>)Array.Empty<AudioData>();
    public PlaybackMode Mode => GlobalData.Inst.ActiveFunctionPreset?.PlaybackMode ?? PlaybackMode.Single;
    public bool IsPlaying => Tracks.Any(x => x.IsPlaying);
    public bool IsPaused => Tracks.Any(x => x.IsPaused);
    private void PrepareQueue(bool changed = false) => queue.Synchronize(Tracks, Mode, changed);
    public void NotifyChanged()
    {
        PrepareQueue();
        Changed?.Invoke();
    }

    internal void TracksChanged()
    {
        PrepareQueue(changed: true);
        NotifyChanged();
    }

    public void GamePresetChanged() => TracksChanged();

    public void SetMode(PlaybackMode mode)
    {
        var preset = GlobalData.Inst.ActiveFunctionPreset;
        if (preset == null || preset.PlaybackMode == mode) return;
        preset.PlaybackMode = mode;
        preset.Save();
        PrepareQueue();
        if (mode != PlaybackMode.Single)
            foreach (var track in Tracks.Where(x => x != Current).ToArray()) track.Stop();
        NotifyChanged();
    }

    public void Toggle(AudioData audio)
    {
        PrepareQueue();
        if (!Tracks.Contains(audio)) return;
        queue.Select(audio);
        if (audio.IsPlaying) audio.Pause();
        else
        {
            if (Mode != PlaybackMode.Single)
                foreach (var track in Tracks.Where(x => x != audio).ToArray()) track.Stop();
            StartTrack(audio);
        }
        NotifyChanged();
    }

    public void PauseOrResume()
    {
        PrepareQueue();
        if (IsPlaying)
        {
            foreach (var track in Tracks.Where(x => x.IsPlaying).ToArray()) track.Pause();
        }
        else
        {
            var paused = Tracks.Where(x => x.IsPaused).ToArray();
            if (paused.Length > 0)
            {
                foreach (var track in paused) StartTrack(track);
            }
            else
            {
                var track = queue.EnsureCurrent();
                if (track != null) StartTrack(track);
            }
        }
        NotifyChanged();
    }

    // Lifecycle cleanup and track transitions still release audio devices/readers.
    internal void StopPresetAudio()
    {
        foreach (var track in Tracks.ToArray()) track.Stop();
        NotifyChanged();
    }

    public void Reset()
    {
        StopPresetAudio();
        queue.Clear();
        NotifyChanged();
    }

    public void Next() => Move(1);
    public void Previous() => Move(-1);

    private void Move(int direction)
    {
        PrepareQueue();
        var next = queue.Move(direction);
        if (next == null) return;
        StopPresetAudio();
        StartTrack(next);
        NotifyChanged();
    }

    private static void StartTrack(AudioData track)
    {
        try { track.Start(); }
        catch (Exception exception)
        {
            track.Stop();
            AppLog.Error(exception, "播放音频失败。", "PlaybackController");
            MainWindow.Inst.AddNotification(
                () => Resources.Language.LanguageManager.Inst.GetString("通知"),
                () => Resources.Language.LanguageManager.Inst.GetString("播放失败"),
                Pages.LabelStatus.Error, 4);
        }
    }

    internal void OnCompleted(AudioData audio)
    {
        PrepareQueue();
        if (audio == Current)
        {
            var next = queue.Complete();
            if (next != null)
            {
                StopPresetAudio();
                StartTrack(next);
            }
        }
        NotifyChanged();
    }

    internal void OnStopped(AudioData audio) => NotifyChanged();
}
