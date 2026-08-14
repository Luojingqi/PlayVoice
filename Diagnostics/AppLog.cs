#nullable enable

using PlayVoice.Pages;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace PlayVoice.Diagnostics;

internal enum AppLogLevel
{
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// 应用统一日志入口。日志同步写入安装目录下的 Logs 文件夹，
/// 固定保留当前会话和两个历史会话。
/// </summary>
internal static class AppLog
{
    private const string LogDirectoryName = "Logs";
    private const string FileBaseName = "PlayVoice";
    private const int RetainedSessionCount = 3;

    private static readonly object SyncRoot = new();
    private static readonly string SessionId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private static StreamWriter? writer;
    private static FileStream? logStream;
    private static TextWriter? originalOut;
    private static TextWriter? originalError;
    private static ConsoleLogWriter? consoleOutWriter;
    private static ConsoleLogWriter? consoleErrorWriter;
    private static AppTraceListener? traceListener;
    private static bool initialized;
    private static bool sessionEnded;

    public static string LogDirectoryPath { get; } =
        Path.Combine(AppContext.BaseDirectory, LogDirectoryName);

    public static string CurrentLogPath { get; } =
        Path.Combine(LogDirectoryPath, $"{FileBaseName}.log");

    public static string CurrentDumpPath { get; } =
        Path.Combine(LogDirectoryPath, $"{FileBaseName}.dmp");

    public static bool IsInitialized
    {
        get
        {
            lock (SyncRoot)
                return initialized;
        }
    }

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (initialized) return;

            Directory.CreateDirectory(LogDirectoryPath);
            RotateSessionFiles();
            sessionEnded = false;

            logStream = new FileStream(
                CurrentLogPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);
            writer = new StreamWriter(logStream, new UTF8Encoding(false))
            {
                AutoFlush = true
            };
            initialized = true;

            originalOut = Console.Out;
            originalError = Console.Error;
            consoleOutWriter = new ConsoleLogWriter(
                originalOut,
                AppLogLevel.Information,
                "Console.Out");
            consoleErrorWriter = new ConsoleLogWriter(
                originalError,
                AppLogLevel.Error,
                "Console.Error");
            Console.SetOut(consoleOutWriter);
            Console.SetError(consoleErrorWriter);

            traceListener = new AppTraceListener();
            Trace.Listeners.Add(traceListener);

            WriteSessionStart();
        }
    }

    public static void Debug(string message, string source = "Application") =>
        Write(AppLogLevel.Debug, message, null, source);

    public static void Information(string message, string source = "Application") =>
        Write(AppLogLevel.Information, message, null, source);

    public static void Warning(string message, string source = "Application") =>
        Write(AppLogLevel.Warning, message, null, source);

    public static void Warning(Exception exception, string message, string source = "Application") =>
        Write(AppLogLevel.Warning, message, exception, source);

    public static void Error(string message, string source = "Application") =>
        Write(AppLogLevel.Error, message, null, source);

    public static void Error(Exception exception, string message, string source = "Application") =>
        Write(AppLogLevel.Error, message, exception, source);

    public static void Fatal(string message, string source = "Application") =>
        Write(AppLogLevel.Fatal, message, null, source);

    public static void Fatal(Exception exception, string message, string source = "Application") =>
        Write(AppLogLevel.Fatal, message, exception, source);

    public static void Notification(
        string title,
        string message,
        LabelStatus status)
    {
        string logMessage = $"通知 | Status={status} | Title={title} | Message={message}";
        AppLogLevel level = status switch
        {
            LabelStatus.Error => AppLogLevel.Error,
            LabelStatus.Warning => AppLogLevel.Warning,
            _ => AppLogLevel.Information
        };
        Write(level, logMessage, null, "UI.Notification");
    }

    public static void WriteSessionEnd(string outcome, string reason)
    {
        lock (SyncRoot)
        {
            if (!initialized || sessionEnded) return;
            try
            {
                WriteCore(
                    AppLogLevel.Information,
                    $"SESSION END | EndedAt={DateTimeOffset.Now:O} | Outcome={outcome} | Reason={reason}",
                    null,
                    "Application.Lifecycle");
                sessionEnded = true;
                FlushCore(flushToDisk: true);
            }
            catch (Exception writeException)
            {
                ReportInternalFailure("写入会话结束标记", writeException);
            }
        }
    }

    public static void FlushToDisk()
    {
        lock (SyncRoot)
            FlushCore(flushToDisk: true);
    }

    public static void Shutdown()
    {
        ConsoleLogWriter? outWriter;
        ConsoleLogWriter? errorWriter;

        lock (SyncRoot)
        {
            if (!initialized) return;
            outWriter = consoleOutWriter;
            errorWriter = consoleErrorWriter;
        }

        // 先提交尚未换行的控制台内容，不能在日志锁内调用，避免锁顺序反转。
        outWriter?.FlushPendingLine();
        errorWriter?.FlushPendingLine();

        lock (SyncRoot)
        {
            if (!initialized) return;

            FlushCore(flushToDisk: true);

            if (originalOut != null)
                Console.SetOut(originalOut);
            if (originalError != null)
                Console.SetError(originalError);
            if (traceListener != null)
                Trace.Listeners.Remove(traceListener);

            writer?.Dispose();
            logStream?.Dispose();
            writer = null;
            logStream = null;
            consoleOutWriter = null;
            consoleErrorWriter = null;
            traceListener = null;
            initialized = false;
        }
    }

    internal static void Write(
        AppLogLevel level,
        string message,
        Exception? exception,
        string source)
    {
        lock (SyncRoot)
        {
            if (!initialized) return;
            try
            {
                WriteCore(level, message, exception, source);
                if (level is AppLogLevel.Error or AppLogLevel.Fatal)
                    FlushCore(flushToDisk: true);
            }
            catch (Exception writeException)
            {
                ReportInternalFailure("写入", writeException);
            }
        }
    }

    private static void WriteCore(
        AppLogLevel level,
        string message,
        Exception? exception,
        string source)
    {
        if (writer == null) return;

        string levelText = level switch
        {
            AppLogLevel.Debug => "DEBUG",
            AppLogLevel.Information => "INFO ",
            AppLogLevel.Warning => "WARN ",
            AppLogLevel.Error => "ERROR",
            AppLogLevel.Fatal => "FATAL",
            _ => level.ToString().ToUpperInvariant()
        };

        writer.WriteLine(
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
            $"[{levelText}] [Session:{SessionId}] " +
            $"[Process:{Environment.ProcessId}] " +
            $"[Thread:{Environment.CurrentManagedThreadId}] " +
            $"[Source:{source}] {message}");

        if (exception != null)
            writer.WriteLine(exception);
    }

    private static void FlushCore(bool flushToDisk)
    {
        try
        {
            writer?.Flush();
            if (flushToDisk)
                logStream?.Flush(flushToDisk: true);
        }
        catch (Exception exception)
        {
            ReportInternalFailure("刷新", exception);
        }
    }

    private static void ReportInternalFailure(string operation, Exception exception)
    {
        try
        {
            originalError?.WriteLine($"PlayVoice 日志{operation}失败：{exception}");
        }
        catch
        {
            // 日志故障不能触发递归异常。
        }
    }

    private static void WriteSessionStart()
    {
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "Unknown";
        string previousOutcome = InspectPreviousSessionOutcome();

        WriteCore(
            AppLogLevel.Information,
            "SESSION START | " +
            $"StartedAt={DateTimeOffset.Now:O} | " +
            $"Version={version} | Runtime={Environment.Version} | " +
            $"OS={Environment.OSVersion} | Architecture={RuntimeInformation.ProcessArchitecture} | " +
            $"Culture={CultureInfo.CurrentUICulture.Name} | PreviousSession={previousOutcome}",
            null,
            "Application.Lifecycle");

        if (previousOutcome is "UnexpectedTermination" or "CrashedWithDump"
            || previousOutcome.Contains("Crash", StringComparison.OrdinalIgnoreCase))
        {
            WriteCore(
                AppLogLevel.Warning,
                $"检测到上一会话异常结束。PreviousSession={previousOutcome}",
                null,
                "Application.Lifecycle");
        }
        FlushCore(flushToDisk: true);
    }

    private static string InspectPreviousSessionOutcome()
    {
        string previousLogPath = GetSessionFilePath(1, ".log");
        if (!File.Exists(previousLogPath)) return "None";

        try
        {
            const int tailLength = 32 * 1024;
            using var stream = new FileStream(
                previousLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            int bytesToRead = (int)Math.Min(stream.Length, tailLength);
            stream.Seek(-bytesToRead, SeekOrigin.End);
            byte[] buffer = new byte[bytesToRead];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string tail = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            int sessionEndIndex = tail.LastIndexOf("SESSION END", StringComparison.Ordinal);
            if (sessionEndIndex >= 0)
            {
                int outcomeIndex = tail.IndexOf("Outcome=", sessionEndIndex, StringComparison.Ordinal);
                if (outcomeIndex >= 0)
                {
                    outcomeIndex += "Outcome=".Length;
                    int outcomeEndIndex = tail.IndexOf(" |", outcomeIndex, StringComparison.Ordinal);
                    if (outcomeEndIndex < 0)
                        outcomeEndIndex = tail.IndexOfAny(['\r', '\n'], outcomeIndex);
                    if (outcomeEndIndex < 0)
                        outcomeEndIndex = tail.Length;
                    return tail[outcomeIndex..outcomeEndIndex];
                }
                return "Recorded";
            }
            if (File.Exists(GetSessionFilePath(1, ".dmp")))
                return "CrashedWithDump";
            return "UnexpectedTermination";
        }
        catch (Exception exception)
        {
            return $"Unknown({exception.GetType().Name})";
        }
    }

    private static void RotateSessionFiles()
    {
        foreach (string extension in new[] { ".log", ".dmp" })
        {
            string oldestPath = GetSessionFilePath(RetainedSessionCount - 1, extension);
            if (File.Exists(oldestPath))
                File.Delete(oldestPath);

            for (int index = RetainedSessionCount - 2; index >= 0; index--)
            {
                string sourcePath = GetSessionFilePath(index, extension);
                if (!File.Exists(sourcePath)) continue;

                string destinationPath = GetSessionFilePath(index + 1, extension);
                File.Move(sourcePath, destinationPath, overwrite: true);
            }
        }
    }

    private static string GetSessionFilePath(int sessionIndex, string extension)
    {
        string suffix = sessionIndex == 0 ? string.Empty : $".{sessionIndex}";
        return Path.Combine(LogDirectoryPath, $"{FileBaseName}{suffix}{extension}");
    }
}
