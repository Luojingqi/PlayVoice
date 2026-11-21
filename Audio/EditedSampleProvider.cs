using NAudio.Wave;

namespace PlayVoice.Audio;

/// <summary>Read-only, frame-aligned trimming and linear gain envelopes before resampling.</summary>
public sealed class EditedSampleProvider : ISampleProvider
{
    private readonly AudioFileReader reader;
    private readonly long totalFrames;
    private readonly double fadeInFrames;
    private readonly double fadeOutFrames;
    private long position;
    public WaveFormat WaveFormat => reader.WaveFormat;

    public EditedSampleProvider(AudioFileReader reader, AudioEditSettings settings, double resumeSeconds = 0)
    {
        this.reader = reader;
        settings ??= new();
        settings.Validate(reader.TotalTime.TotalSeconds, reader.WaveFormat.SampleRate);
        if (!double.IsFinite(resumeSeconds) || resumeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(resumeSeconds));
        long startFrame = (long)Math.Round(settings.StartSeconds * WaveFormat.SampleRate);
        long endFrame = (long)Math.Round((settings.EndSeconds ?? reader.TotalTime.TotalSeconds) * WaveFormat.SampleRate);
        totalFrames = endFrame - startFrame;
        if (totalFrames <= 0) throw new ArgumentException("Selection is shorter than one sample frame.");
        position = Math.Min(totalFrames, (long)Math.Round(resumeSeconds * WaveFormat.SampleRate));
        reader.Position = (startFrame + position) * WaveFormat.BlockAlign;
        fadeInFrames = settings.FadeInSeconds * WaveFormat.SampleRate;
        fadeOutFrames = settings.FadeOutSeconds * WaveFormat.SampleRate;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset > buffer.Length - count || count % WaveFormat.Channels != 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        int requested = (int)Math.Min(count, (totalFrames - position) * WaveFormat.Channels);
        int read = reader.Read(buffer, offset, requested);
        for (int frame = 0; frame < read / WaveFormat.Channels; frame++)
        {
            double gain = (fadeInFrames > 0 ? Math.Min(1, position / fadeInFrames) : 1)
                * (fadeOutFrames > 0 ? Math.Min(1, Math.Max(0, totalFrames - 1 - position) / fadeOutFrames) : 1);
            for (int channel = 0; channel < WaveFormat.Channels; channel++)
                buffer[offset + frame * WaveFormat.Channels + channel] *= (float)gain;
            position++;
        }
        return read;
    }
}
