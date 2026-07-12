using Microsoft.Win32;
using PlayVoice.Hotkey;
using PlayVoice.Resources.Themes;
using Steamworks;
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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SetAlignment();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            new HotkeyManager();
            HotkeyManager.Inst.Start();
            this.DispatcherUnhandledException += (o, e) =>
            {
                HotkeyManager.Inst.Stop();
            };
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
                field.SetValue(null, false);
                ifLeft = SystemParameters.MenuDropAlignment;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            HotkeyManager.Inst.Stop();
            //SteamClient.Init(480);
            SteamClient.Shutdown();
        }


    }

}
