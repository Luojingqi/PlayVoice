#nullable enable

using System.Diagnostics;
using System.Text;

namespace PlayVoice.Diagnostics;

internal sealed class AppTraceListener : TraceListener
{
    private readonly object syncRoot = new();
    private readonly StringBuilder pendingLine = new();

    public override void Write(string? message)
    {
        if (message == null) return;
        lock (syncRoot)
            pendingLine.Append(message);
    }

    public override void WriteLine(string? message)
    {
        lock (syncRoot)
        {
            if (message != null)
                pendingLine.Append(message);
            if (pendingLine.Length == 0) return;
            AppLog.Debug(pendingLine.ToString(), "Trace");
            pendingLine.Clear();
        }
    }

    public override void Flush()
    {
        lock (syncRoot)
        {
            if (pendingLine.Length == 0) return;
            AppLog.Debug(pendingLine.ToString(), "Trace");
            pendingLine.Clear();
        }
    }
}
