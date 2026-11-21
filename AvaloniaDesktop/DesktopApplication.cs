using Avalonia.Threading;
namespace PlayVoice;
// Transitional UI-thread bridge for the shared audio/device core; no WPF assembly is loaded.
internal sealed class DesktopApplication
{
    public static DesktopApplication Current { get; } = new();
    public DesktopDispatcher Dispatcher { get; } = new();
    internal sealed class DesktopDispatcher
    {
        public bool HasShutdownStarted { get; set; }
        public bool CheckAccess() => Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
        public void BeginInvoke(Action action) { if (!HasShutdownStarted) Avalonia.Threading.Dispatcher.UIThread.Post(action); }
        public void Invoke(Action action) { if (!HasShutdownStarted) Avalonia.Threading.Dispatcher.UIThread.Invoke(action); }
    }
}
