using NAudio.Wave;

namespace PlayVoice.Audio;

public enum TrackPlaybackState { Stopped, Playing, Paused }
[Flags]
public enum PlaybackRoute { None = 0, Physical = 1, Virtual = 2 }

public readonly record struct TrackPlaybackSnapshot(string Name, TrackPlaybackState State,
    PlaybackRoute Routes, TimeSpan ClipStart, TimeSpan Duration, TimeSpan Position)
{
    public double Progress => Duration > TimeSpan.Zero ? Position / Duration : 0;
    public string StatusKey => State switch
    {
        TrackPlaybackState.Playing => "正在播放",
        TrackPlaybackState.Paused => "已暂停",
        _ => "已停止"
    };
}

// Count samples handed to the output mixer, after resampling. File readers can be
// several buffers ahead. Each output has its own clock and one EOF notification.
internal sealed class PlaybackOutput(ISampleProvider source, TimeSpan start, Action<PlaybackOutput> ended) : ISampleProvider
{
    private long samplesRead;
    private int finished;
    public WaveFormat WaveFormat => source.WaveFormat;
    public bool IsEnded => Volatile.Read(ref finished) != 0;
    public TimeSpan Position => start + TimeSpan.FromSeconds(
        (double)Interlocked.Read(ref samplesRead) / WaveFormat.Channels / WaveFormat.SampleRate);

    public int Read(float[] buffer, int offset, int count)
    {
        if (count == 0 || IsEnded) return 0;
        int read = source.Read(buffer, offset, count);
        Interlocked.Add(ref samplesRead, read);
        if (read < count && Interlocked.Exchange(ref finished, 1) == 0)
            ended(this);
        return read;
    }
}
