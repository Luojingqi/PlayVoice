#nullable enable

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace PlayVoice.Audio;

/// <summary>
/// Buffers microphone bytes in the format negotiated with WASAPI and exposes
/// the same audio as normalized 32-bit floating-point samples.
/// </summary>
internal sealed class MicrophoneSampleBuffer
{
    private readonly BufferedWaveProvider captureBuffer;
    private readonly bool discardOnBufferOverflow;

    public WaveFormat CaptureFormat { get; }

    public ISampleProvider SampleProvider { get; }

    public WaveFormat SampleFormat => SampleProvider.WaveFormat;

    public MicrophoneSampleBuffer(
        WaveFormat captureFormat,
        bool readFully,
        bool discardOnBufferOverflow,
        TimeSpan? bufferDuration = null)
    {
        ArgumentNullException.ThrowIfNull(captureFormat);
        captureFormat = captureFormat.AsStandardWaveFormat();
        if (captureFormat.SampleRate <= 0)
            throw new ArgumentException("麦克风采样率必须大于 0。", nameof(captureFormat));
        if (captureFormat.Channels <= 0)
            throw new ArgumentException("麦克风声道数必须大于 0。", nameof(captureFormat));
        if (captureFormat.BlockAlign <= 0)
            throw new ArgumentException("麦克风音频帧大小必须大于 0。", nameof(captureFormat));

        CaptureFormat = captureFormat;
        captureBuffer = new BufferedWaveProvider(captureFormat)
        {
            // Padding raw bytes is unsafe for unsigned 8-bit PCM, where 0x00
            // represents full negative amplitude instead of silence.
            ReadFully = false,
            DiscardOnBufferOverflow = false,
        };
        if (bufferDuration.HasValue)
        {
            if (bufferDuration.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(bufferDuration));

            double requestedFrameCount = Math.Ceiling(
                bufferDuration.Value.TotalSeconds * captureFormat.SampleRate);
            double maximumFrameCount = int.MaxValue / (double)captureFormat.BlockAlign;
            if (requestedFrameCount > maximumFrameCount)
                throw new ArgumentOutOfRangeException(nameof(bufferDuration));

            int frameCount = Math.Max(1, checked((int)requestedFrameCount));
            captureBuffer.BufferLength = checked(frameCount * captureFormat.BlockAlign);
        }
        this.discardOnBufferOverflow = discardOnBufferOverflow;

        ISampleProvider convertedProvider = captureBuffer.ToSampleProvider();
        SampleProvider = readFully
            ? new ZeroPadSampleProvider(convertedProvider)
            : convertedProvider;

        WaveFormat sampleFormat = SampleProvider.WaveFormat;
        if (sampleFormat.Encoding != WaveFormatEncoding.IeeeFloat
            || sampleFormat.BitsPerSample != 32
            || sampleFormat.SampleRate != captureFormat.SampleRate
            || sampleFormat.Channels != captureFormat.Channels)
        {
            throw new NotSupportedException(
                $"无法把麦克风格式转换为等采样率、等声道的 32 位浮点格式。" +
                $" Capture={captureFormat}; Sample={sampleFormat}");
        }
    }

    /// <summary>
    /// Adds complete capture frames and returns the number of floating-point
    /// samples represented by those frames, including all channels.
    /// </summary>
    public int AddSamples(byte[] source, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (offset < 0 || offset > source.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || count > source.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count % CaptureFormat.BlockAlign != 0)
        {
            throw new InvalidDataException(
                $"麦克风采集数据不是完整音频帧。" +
                $" Bytes={count}; BlockAlign={CaptureFormat.BlockAlign}");
        }

        if (count > captureBuffer.BufferLength)
        {
            throw new InvalidDataException(
                $"单次麦克风采集数据超过缓冲容量。" +
                $" Bytes={count}; Capacity={captureBuffer.BufferLength}");
        }
        int bufferedBytes = captureBuffer.BufferedBytes;
        if (bufferedBytes > captureBuffer.BufferLength - count)
        {
            if (discardOnBufferOverflow)
            {
                // A real-time consumer that falls behind should resume from the
                // newest audio instead of preserving stale microphone input.
                captureBuffer.ClearBuffer();
            }
            else
            {
                throw new InvalidDataException(
                    $"麦克风采集缓冲溢出。" +
                    $" Buffered={bufferedBytes}; Added={count}; " +
                    $"Capacity={captureBuffer.BufferLength}");
            }
        }

        captureBuffer.AddSamples(source, offset, count);
        int frameCount = count / CaptureFormat.BlockAlign;
        return checked(frameCount * SampleFormat.Channels);
    }

    public void Clear() => captureBuffer.ClearBuffer();

    private sealed class ZeroPadSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        public ZeroPadSampleProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            if (samplesRead < 0 || samplesRead > count)
            {
                throw new InvalidDataException(
                    $"麦克风格式转换器返回了无效样本数。" +
                    $" Read={samplesRead}; Requested={count}");
            }
            if (samplesRead < count)
                Array.Clear(buffer, offset + samplesRead, count - samplesRead);
            return count;
        }
    }
}
