using PlayVoice.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace PlayVoice.Pages.Preset;

/// <summary>Shell file drops also work when the main window is elevated.</summary>
internal sealed class NativeFileDrop : IDisposable
{
    private readonly FrameworkElement target;
    private readonly Func<string[], Task> import;
    private readonly HwndSource source;
    public NativeFileDrop(FrameworkElement target, Func<string[], Task> import)
    {
        this.target = target;
        this.import = import;
        source = (HwndSource)PresentationSource.FromVisual(target);
        if (source == null) return;
        source.AddHook(WindowProc);
        ChangeWindowMessageFilterEx(source.Handle, 0x0233, 1, IntPtr.Zero);
        ChangeWindowMessageFilterEx(source.Handle, 0x0049, 1, IntPtr.Zero);
        DragAcceptFiles(source.Handle, true);
    }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0233) return IntPtr.Zero;
        handled = true;
        string[] files = Array.Empty<string>();
        try
        {
            if (!target.IsVisible || !target.IsEnabled || !DragQueryPoint(wParam, out var point)) return IntPtr.Zero;
            ClientToScreen(hwnd, ref point);
            var local = target.PointFromScreen(new Point(point.X, point.Y));
            if (!new Rect(target.RenderSize).Contains(local)) return IntPtr.Zero;
            uint count = DragQueryFile(wParam, uint.MaxValue, null, 0);
            files = new string[count];
            for (uint i = 0; i < count; i++)
            {
                uint length = DragQueryFile(wParam, i, null, 0);
                var buffer = new StringBuilder((int)length + 1);
                DragQueryFile(wParam, i, buffer, length + 1);
                files[i] = buffer.ToString();
            }
        }
        finally { DragFinish(wParam); }
        if (files.Length > 0) ImportAsync(files).Forget("NativeFileDrop.Import");
        return IntPtr.Zero;
    }
    private async Task ImportAsync(string[] files) => await import(files);
    public void Dispose()
    {
        if (source == null || source.IsDisposed) return;
        source.RemoveHook(WindowProc);
        DragAcceptFiles(source.Handle, false);
        ChangeWindowMessageFilterEx(source.Handle, 0x0233, 0, IntPtr.Zero);
        ChangeWindowMessageFilterEx(source.Handle, 0x0049, 0, IntPtr.Zero);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [DllImport("shell32.dll")] private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder file, uint size);
    [DllImport("shell32.dll")] private static extern bool DragQueryPoint(IntPtr drop, out NativePoint point);
    [DllImport("shell32.dll")] private static extern void DragFinish(IntPtr drop);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr change);
}
