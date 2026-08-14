#nullable enable

using System.Diagnostics;
using System.IO;

namespace PlayVoice.Diagnostics;

internal static class TerminationCoordinator
{
    private static int terminating;

    public static bool IsTerminating => Volatile.Read(ref terminating) != 0;

    public static void ForceExit(string reason, Exception? exception = null)
    {
        if (Interlocked.Exchange(ref terminating, 1) != 0)
            return;

        try
        {
            if (exception == null)
                AppLog.Fatal($"程序请求强制退出。Reason={reason}", "Application.Termination");
            else
                AppLog.Fatal(exception, $"程序因致命异常强制退出。Reason={reason}", "Application.Termination");

            AppLog.FlushToDisk();

            if (CrashDumpWriter.TryWrite(out string? dumpError))
            {
                long dumpSize = new FileInfo(AppLog.CurrentDumpPath).Length;
                AppLog.Information(
                    $"Dump 已生成。Path={AppLog.CurrentDumpPath} | Size={dumpSize}",
                    "Application.Termination");
            }
            else
            {
                AppLog.Error(
                    $"Dump 生成失败。Reason={dumpError}",
                    "Application.Termination");
            }

            AppLog.WriteSessionEnd("ForcedExit", reason);
            AppLog.Shutdown();
        }
        catch (Exception terminationException)
        {
            try
            {
                AppLog.Fatal(
                    terminationException,
                    "执行强制退出流程时发生异常。",
                    "Application.Termination");
                AppLog.FlushToDisk();
            }
            catch
            {
                // 最终终止路径不能再次失败。
            }
        }
        finally
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}
