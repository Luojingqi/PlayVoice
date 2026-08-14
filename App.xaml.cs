using Microsoft.Win32;
using PlayVoice.Diagnostics;
using PlayVoice.Hotkey;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using Steamworks;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace PlayVoice
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static int SteamAppId = 0;
        private Mutex singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: @"Global\PlayVoice.Application",
                createdNew: out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    LanguageManager.GetBootstrapString("程序已经在运行"),
                    "PlayVoice",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Process.GetCurrentProcess().Kill();
                return;
            }

            try
            {
                AppLog.Initialize();
                GlobalExceptionHandler.Register(this);
                AppLog.Information("应用程序开始启动。", "Application.Startup");

                base.OnStartup(e);
                SetAlignment();
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                new HotkeyManager();
                HotkeyManager.Inst.Start();
            }
            catch (Exception exception)
            {
                if (!AppLog.IsInitialized)
                {
                    MessageBox.Show(
                        $"{LanguageManager.GetBootstrapString("日志初始化失败")}\n\n{exception.Message}",
                        "PlayVoice",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Process.GetCurrentProcess().Kill();
                    return;
                }

                TerminationCoordinator.ForceExit("StartupFailed", exception);
            }
        }


        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
                if (ThemeManager.NowTheme.Theme == ThemeManager.ThemeEnum.System)
                    ThemeManager.SwitchTheme(ThemeManager.ThemeEnum.System);
        }

        public void SetAlignment()
        {
            var ifLeft = SystemParameters.MenuDropAlignment;
            if (ifLeft)
            {
                var t = typeof(SystemParameters);
                var field = t.GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    AppLog.Warning(
                        "无法获取菜单对齐字段，将保留 Windows 默认设置。",
                        "Application.Alignment");
                    return;
                }
                field.SetValue(null, false);
                ifLeft = SystemParameters.MenuDropAlignment;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                if (GlobalData.Inst != null)
                {
                    GlobalData.Inst.TryRun(false);
                    GlobalData.Inst.DisposeAudioPresetForExit();
                }
                HotkeyManager.Inst?.Stop();
                SteamClient.Shutdown();
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    exception,
                    "应用正常退出时清理资源失败。",
                    "Application.Exit");
            }
            finally
            {
                if (!TerminationCoordinator.IsTerminating)
                    AppLog.WriteSessionEnd("NormalExit", $"ExitCode={e.ApplicationExitCode}");
                AppLog.Shutdown();
                try
                {
                    singleInstanceMutex?.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // 互斥锁并非由当前线程持有时无需释放。
                }
                singleInstanceMutex?.Dispose();
            }
            base.OnExit(e);
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            AppLog.Information(
                $"Windows 会话即将结束。Reason={e.ReasonSessionEnding}",
                "Application.SessionEnding");
            PlayVoice.MainWindow.Inst?.PrepareForExit();
            base.OnSessionEnding(e);
        }


    }

}
