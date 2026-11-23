using System.Windows.Media;
using System.Windows.Media.Animation;
using PlayVoice.Audio;

namespace PlayVoice.Pages.Playback;

internal static class PlaybackAnimation
{
    internal static void Synchronize(ScaleTransform scale, TrackPlaybackSnapshot snapshot)
    {
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.ScaleX = snapshot.Progress;
        if (snapshot.State == TrackPlaybackState.Playing && snapshot.Position < snapshot.Duration)
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(snapshot.Progress, 1, snapshot.Duration - snapshot.Position));
    }
}
