#nullable enable

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PlayVoice.Diagnostics;

internal static class CrashDumpWriter
{
    private const int ExceptionExecuteHandler = 1;
    private static readonly object SyncRoot = new();
    private static readonly UnhandledExceptionFilter NativeExceptionFilter =
        HandleNativeException;
    private static int nativeExceptionHandled;

    public static void RegisterNativeExceptionHandler()
    {
        SetUnhandledExceptionFilter(NativeExceptionFilter);
    }

    public static bool TryWrite(
        out string? error,
        IntPtr exceptionPointers = default,
        uint exceptionThreadId = 0)
    {
        lock (SyncRoot)
        {
            IntPtr exceptionInformationPointer = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(AppLog.LogDirectoryPath);
                using var process = Process.GetCurrentProcess();
                using var dumpStream = new FileStream(
                    AppLog.CurrentDumpPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);

                if (exceptionPointers != IntPtr.Zero)
                {
                    var exceptionInformation = new MiniDumpExceptionInformation
                    {
                        ThreadId = exceptionThreadId == 0
                            ? GetCurrentThreadId()
                            : exceptionThreadId,
                        ExceptionPointers = exceptionPointers,
                        ClientPointers = false
                    };
                    exceptionInformationPointer = Marshal.AllocHGlobal(
                        Marshal.SizeOf<MiniDumpExceptionInformation>());
                    Marshal.StructureToPtr(
                        exceptionInformation,
                        exceptionInformationPointer,
                        fDeleteOld: false);
                }

                bool succeeded = MiniDumpWriteDump(
                    process.Handle,
                    (uint)process.Id,
                    dumpStream.SafeFileHandle,
                    MiniDumpType.WithThreadInfo |
                    MiniDumpType.WithUnloadedModules |
                    MiniDumpType.WithIndirectlyReferencedMemory,
                    exceptionInformationPointer,
                    IntPtr.Zero,
                    IntPtr.Zero);
                dumpStream.Flush(flushToDisk: true);

                if (!succeeded)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                try
                {
                    if (File.Exists(AppLog.CurrentDumpPath))
                        File.Delete(AppLog.CurrentDumpPath);
                }
                catch
                {
                    // 保留原始 Dump 失败原因。
                }
                return false;
            }
            finally
            {
                if (exceptionInformationPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(exceptionInformationPointer);
            }
        }
    }

    private static int HandleNativeException(IntPtr exceptionPointers)
    {
        if (Interlocked.Exchange(ref nativeExceptionHandled, 1) != 0)
            return ExceptionExecuteHandler;

        try
        {
            AppLog.Fatal("捕获到原生未处理异常。", "GlobalException.Native");
            AppLog.FlushToDisk();
            if (TryWrite(
                out string? error,
                exceptionPointers,
                GetCurrentThreadId()))
            {
                AppLog.Fatal(
                    $"原生崩溃 Dump 已生成：{AppLog.CurrentDumpPath}",
                    "GlobalException.Native");
            }
            else
            {
                AppLog.Fatal(
                    $"原生崩溃 Dump 生成失败：{error}",
                    "GlobalException.Native");
            }
            AppLog.WriteSessionEnd("NativeCrash", "UnhandledNativeException");
            AppLog.FlushToDisk();
        }
        catch
        {
            // 原生崩溃处理器不得再次抛出异常。
        }

        return ExceptionExecuteHandler;
    }

    [Flags]
    private enum MiniDumpType : uint
    {
        WithUnloadedModules = 0x00000020,
        WithIndirectlyReferencedMemory = 0x00000040,
        WithThreadInfo = 0x00001000
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MiniDumpExceptionInformation
    {
        public uint ThreadId;
        public IntPtr ExceptionPointers;

        [MarshalAs(UnmanagedType.Bool)]
        public bool ClientPointers;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int UnhandledExceptionFilter(IntPtr exceptionPointers);

    [DllImport("Dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr processHandle,
        uint processId,
        SafeFileHandle fileHandle,
        MiniDumpType dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr SetUnhandledExceptionFilter(
        UnhandledExceptionFilter filter);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
