using NAudio.Wave;
using PlayVoice.Audio;
using System.Security.Cryptography;

internal static class AudioEditingChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        string path = Path.Combine(Path.GetTempPath(), "PlayVoice-edit-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            using (var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(1000, 2)))
                writer.WriteSamples(Enumerable.Repeat(1f, 2000).ToArray(), 0, 2000);
            byte[] original = SHA256.HashData(File.ReadAllBytes(path));
            var edit = new AudioEditSettings { StartSeconds = .2, EndSeconds = .7, FadeInSeconds = .1, FadeOutSeconds = .1 };
            using (var reader = new AudioFileReader(path))
            {
                var provider = new EditedSampleProvider(reader, edit);
                Check(reader.Position == 1600, "Trim must seek to the selected source frame");
                var samples = new float[1200];
                int read = provider.Read(samples, 0, samples.Length);
                Check(read == 1000, "Trim must bound output to 500 stereo frames");
                Check(samples[0] == 0 && samples[1] == 0 && samples[998] == 0 && samples[999] == 0, "Fade endpoints");
                Check(Math.Abs(samples[100] - .5f) < .02f && samples[100] == samples[101], "Frame based stereo envelope");
                Check(samples[400] == 1 && provider.Read(samples, 0, samples.Length) == 0, "Interior level and EOF");
            }
            using (var reader = new AudioFileReader(path))
            {
                var provider = new EditedSampleProvider(reader, edit, .05);
                var block = new float[100];
                provider.Read(block, 0, block.Length);
                Check(Math.Abs(block[0] - .5) < .02, "Resume must retain envelope position");
            }
            using (var reader = new AudioFileReader(path))
            {
                var provider = new EditedSampleProvider(reader, null);
                var block = new float[2400];
                Check(provider.Read(block, 0, block.Length) == 2000 && block[0] == 1, "Legacy unedited audio");
            }
            using (var reader = new AudioFileReader(path))
            {
                var provider = new EditedSampleProvider(reader, edit);
                var block = new float[38];
                int total = 0, count;
                while ((count = provider.Read(block, 2, 36)) > 0)
                {
                    for (int i = 2; i < count + 2; i += 2) Check(block[i] == block[i + 1], "Channel alignment across read boundaries");
                    total += count;
                }
                Check(total == 1000, "Small-block output must match full-block trim length");
            }
            foreach (var invalid in new[] { new AudioEditSettings { StartSeconds = .7, EndSeconds = .2 }, new AudioEditSettings { EndSeconds = 2 }, new AudioEditSettings { FadeInSeconds = double.NaN }, new AudioEditSettings { StartSeconds = -.1 } })
            {
                try { invalid.Validate(1); throw new Exception("Invalid edit accepted"); }
                catch (ArgumentException) { }
            }
            var peaks = WaveformPeaks.Read(path, 64, CancellationToken.None);
            try { new AudioEditSettings { EndSeconds = .00001 }.Validate(1, 1000); throw new Exception("Sub-frame selection accepted"); }
            catch (ArgumentException) { }
            Check(peaks.Length == 64 && peaks.All(p => p == 1), "Bounded waveform peaks");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try { WaveformPeaks.Read(path, 64, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Source bytes must never change");
            var config = new AudioDataConfig { Edit = edit };
            var restored = System.Text.Json.JsonSerializer.Deserialize<AudioDataConfig>(System.Text.Json.JsonSerializer.Serialize(config));
            Check(restored!.Edit!.StartSeconds == .2 && restored.Edit.FadeOutSeconds == .1, "Edit metadata roundtrip");
            Console.WriteLine("PASS: stereo trim, fade endpoints, resumed envelope, legacy playback, edit validation, waveform cancellation, metadata roundtrip and original file hash");
        }
        finally { File.Delete(path); }
    }
}
