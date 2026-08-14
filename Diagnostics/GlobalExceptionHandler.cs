#nullable enable

using System.Windows;
using System.Windows.Threading;

namespace PlayVoice.Diagnostics;

internal static class GlobalExceptionHandler
{
    private static int registered;

    public static void Register(Application application)
    {
        if (Interlocked.Exchange(ref registered, 1) != 0) return;

        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        System.Windows.Forms.Application.ThreadException += OnWindowsFormsThreadException;
        CrashDumpWriter.RegisterNativeExceptionHandler();
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        TerminationCoordinator.ForceExit(
            "DispatcherUnhandledException",
            eventArgs.Exception);
    }

    private static void OnUnhandledException(
        object sender,
        UnhandledExceptionEventArgs eventArgs)
    {
        Exception exception = eventArgs.ExceptionObject as Exception
            ?? new Exception($"未知未处理异常对象：{eventArgs.ExceptionObject}");
        TerminationCoordinator.ForceExit(
            eventArgs.IsTerminating
                ? "AppDomainUnhandledExceptionTerminating"
                : "AppDomainUnhandledException",
            exception);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs eventArgs)
    {
        AppLog.Error(
            eventArgs.Exception,
            "检测到未观察的 Task 异常。",
            "GlobalException.TaskScheduler");
        eventArgs.SetObserved();
    }

    private static void OnWindowsFormsThreadException(
        object sender,
        System.Threading.ThreadExceptionEventArgs eventArgs)
    {
        TerminationCoordinator.ForceExit(
            "WindowsFormsThreadException",
            eventArgs.Exception);
    }
}
