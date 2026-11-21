using NAudio.Wave;

namespace PlayVoice.Audio;

internal static class WaveformPeaks
{
    public static float[] Read(string path, int bins, CancellationToken cancellation)
    {
        if (bins is < 16 or > 4096) throw new ArgumentOutOfRangeException(nameof(bins));
        cancellation.ThrowIfCancellationRequested();
        using var reader = new AudioFileReader(path);
        long frames = reader.Length / reader.WaveFormat.BlockAlign;
        if (frames <= 0) throw new ArgumentException("Empty audio.");
        var peaks = new float[bins];
        var buffer = new float[4096 * reader.WaveFormat.Channels];
        long sample = 0;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int i = 0; i < read; i++, sample++)
            {
                int bin = (int)Math.Min(bins - 1, sample / reader.WaveFormat.Channels * bins / frames);
                if (float.IsFinite(buffer[i])) peaks[bin] = Math.Max(peaks[bin], Math.Min(1, Math.Abs(buffer[i])));
            }
        }
        return peaks;
    }
}
