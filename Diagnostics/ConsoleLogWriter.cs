#nullable enable

using System.IO;
using System.Text;

namespace PlayVoice.Diagnostics;

internal sealed class ConsoleLogWriter : TextWriter
{
    private readonly object syncRoot = new();
    private readonly TextWriter originalWriter;
    private readonly AppLogLevel level;
    private readonly string source;
    private readonly StringBuilder pendingLine = new();

    public ConsoleLogWriter(
        TextWriter originalWriter,
        AppLogLevel level,
        string source)
    {
        this.originalWriter = originalWriter;
        this.level = level;
        this.source = source;
    }

    public override Encoding Encoding => originalWriter.Encoding;

    public override void Write(char value)
    {
        lock (syncRoot)
        {
            TryWriteOriginal(value);
            Append(value);
        }
    }

    public override void Write(string? value)
    {
        if (value == null) return;

        lock (syncRoot)
        {
            TryWriteOriginal(value);
            foreach (char character in value)
                Append(character);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        lock (syncRoot)
        {
            try
            {
                originalWriter.Write(buffer, index, count);
            }
            catch
            {
                // 原始控制台不可用时仍继续写应用日志。
            }

            for (int offset = 0; offset < count; offset++)
                Append(buffer[index + offset]);
        }
    }

    public override void Flush()
    {
        lock (syncRoot)
        {
            try
            {
                originalWriter.Flush();
            }
            catch
            {
                // 原始控制台不可用时仍继续写应用日志。
            }
            EmitPendingLine();
        }
    }

    public void FlushPendingLine()
    {
        lock (syncRoot)
            EmitPendingLine();
    }

    private void Append(char character)
    {
        if (character == '\n')
        {
            EmitPendingLine();
            return;
        }

        if (character != '\r')
            pendingLine.Append(character);
    }

    private void EmitPendingLine()
    {
        if (pendingLine.Length == 0) return;
        string line = pendingLine.ToString();
        pendingLine.Clear();
        AppLog.Write(level, line, null, source);
    }

    private void TryWriteOriginal(char value)
    {
        try
        {
            originalWriter.Write(value);
        }
        catch
        {
            // 原始控制台不可用时仍继续写应用日志。
        }
    }

    private void TryWriteOriginal(string value)
    {
        try
        {
            originalWriter.Write(value);
        }
        catch
        {
            // 原始控制台不可用时仍继续写应用日志。
        }
    }
}
