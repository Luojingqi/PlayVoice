using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PlayVoice.Diagnostics;
using PlayVoice.Hotkey;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
namespace PlayVoice;

public sealed partial class DesktopApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppLog.Initialize();
            new LanguageManager();
            ThemeManager.Init();
            new HotkeyManager();
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) => AppLog.Shutdown();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
