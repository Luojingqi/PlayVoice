using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PlayVoice.Audio;

namespace PlayVoice;

public sealed class TrackProgressBar : ProgressBar
{
    protected override Type StyleKeyOverride => typeof(ProgressBar);
    public static readonly DirectProperty<TrackProgressBar, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<TrackProgressBar, bool>(nameof(IsPlaying), bar => bar.IsPlaying);
    private bool playing;
    public bool IsPlaying => playing;
    private AudioData track;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(65) };

    public TrackProgressBar()
    {
        timer.Tick += (_, _) => Value = track?.Snapshot.Progress ?? 0;
        AttachedToVisualTree += (_, _) => Observe((DataContext as TrackRow)?.Audio);
        DetachedFromVisualTree += (_, _) => Observe(null);
        DataContextChanged += (_, _) => { if (VisualRoot != null) Observe((DataContext as TrackRow)?.Audio); };
    }

    private void Observe(AudioData audio)
    {
        if (track != null) track.PlaybackChanged -= Refresh;
        track = audio;
        if (track != null) track.PlaybackChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = track?.Snapshot ?? default;
        SetAndRaise(IsPlayingProperty, ref playing, snapshot.State == TrackPlaybackState.Playing);
        Value = snapshot.Progress;
        if (playing) timer.Start();
        else timer.Stop();
    }
}
