using PlayVoice.Pages.Sidebar;
using PlayVoice.Resources.Language;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Themes;
using Steamworks;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace PlayVoice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Inst { get; private set; }
        private readonly MainViewModel viewModel;
        private readonly System.Windows.Forms.NotifyIcon trayIcon;
        private readonly System.Windows.Forms.ContextMenuStrip trayMenu;
        private readonly System.Windows.Forms.ToolStripMenuItem showTrayMenuItem;
        private readonly System.Windows.Forms.ToolStripMenuItem presetTrayMenuItem;
        private readonly System.Windows.Forms.ToolStripMenuItem exitTrayMenuItem;
        private readonly System.Drawing.Icon trayIconImage;
        private bool isSystemSessionEnding;
        private bool isSwitchingPreset;

        public MainWindow()
        {
            Inst = this;
            new LanguageManager();
            ThemeManager.Init();
            viewModel = new MainViewModel();
            DataContext = viewModel;
            new GlobalData();

            InitializeComponent();
            NavigateToPage(viewModel.SelectedMenu);
            // 监听选择菜单变更，导航到相应页面
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedMenu) && viewModel.SelectedMenu != null)
                {
                    NavigateToPage(viewModel.SelectedMenu);
                }
            };
            ContentFrame.Navigating += ContentFrame_Navigating;

            trayIconImage = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            showTrayMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            presetTrayMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exitTrayMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add(showTrayMenuItem);
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add(presetTrayMenuItem);
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add(exitTrayMenuItem);

            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = trayIconImage ?? System.Drawing.SystemIcons.Application,
                Text = "Play Voice",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => Dispatcher.Invoke(RestoreFromTray);
            showTrayMenuItem.Click += (s, e) => Dispatcher.Invoke(RestoreFromTray);
            presetTrayMenuItem.DropDownOpening += (s, e) => RefreshTrayPresetMenu();
            exitTrayMenuItem.Click += (s, e) => Dispatcher.Invoke(ExitApplication);
            UpdateTrayLanguage();
            LanguageManager.Inst.CultureChanged += (culture, language) => UpdateTrayLanguage();
        }

        private void ContentFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (e.NavigationMode == System.Windows.Navigation.NavigationMode.Back ||
                e.NavigationMode == System.Windows.Navigation.NavigationMode.Forward)
            {
                e.Cancel = true;
            }
        }

        private void NavigateToPage(SidebarItemViewModel item)
        {
            if (!string.IsNullOrEmpty(item.PageUri))
            {
                ContentFrame.Navigate(new Uri(item.PageUri, UriKind.Relative));
            }
        }

        private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeButton.Content = "☐";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeButton.Content = "⧉";
            }
        }
        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!isSystemSessionEnding && GlobalData.Inst.Config.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                ShowInTaskbar = false;
                Hide();
                return;
            }

            if (!isSystemSessionEnding)
            {
                Process.GetCurrentProcess().Kill();
                return;
            }

            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
            trayIconImage?.Dispose();
            base.OnClosing(e);
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            Process.GetCurrentProcess().Kill();
        }

        internal void PrepareForExit()
        {
            isSystemSessionEnding = true;
        }

        private void UpdateTrayLanguage()
        {
            showTrayMenuItem.Text = LanguageManager.Inst.GetString("显示主窗口");
            presetTrayMenuItem.Text = LanguageManager.Inst.GetString("预设");
            exitTrayMenuItem.Text = LanguageManager.Inst.GetString("退出");
        }

        private void RefreshTrayPresetMenu()
        {
            presetTrayMenuItem.DropDownItems.Clear();
            string currentPresetName = GlobalData.Inst.PresetData?.Config?.Name;

            var noPresetItem = new System.Windows.Forms.ToolStripMenuItem(LanguageManager.Inst.GetString("无"))
            {
                Checked = GlobalData.Inst.PresetData == null
            };
            noPresetItem.Click += async (s, e) => await SwitchPresetFromTrayAsync(null);
            presetTrayMenuItem.DropDownItems.Add(noPresetItem);
            presetTrayMenuItem.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());

            try
            {
                foreach (string presetName in PresetDataTool.GetAllPresetName().OrderBy(name => name))
                {
                    var presetItem = new System.Windows.Forms.ToolStripMenuItem(presetName)
                    {
                        Checked = string.Equals(currentPresetName, presetName, StringComparison.OrdinalIgnoreCase)
                    };
                    presetItem.Click += async (s, e) => await SwitchPresetFromTrayAsync(presetName);
                    presetTrayMenuItem.DropDownItems.Add(presetItem);
                }
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                // 预设目录尚未创建时仅显示“无”。
            }
        }

        private async Task SwitchPresetFromTrayAsync(string? presetName)
        {
            if (isSwitchingPreset) return;
            isSwitchingPreset = true;
            presetTrayMenuItem.Enabled = false;

            try
            {
                if (presetName == null)
                {
                    GlobalData.Inst.PresetData = null;
                    return;
                }

                if (string.Equals(GlobalData.Inst.PresetData?.Config?.Name, presetName, StringComparison.OrdinalIgnoreCase))
                    return;

                var presetData = await PresetDataTool.LoadPresetData(presetName);
                if (presetData != null)
                    GlobalData.Inst.PresetData = presetData;
            }
            finally
            {
                isSwitchingPreset = false;
                presetTrayMenuItem.Enabled = true;
            }
        }

        public void AddNotification(string title, string message, Pages.LabelStatus status, float autoDismissSeconds = 5)
        {
            NotificationPanel.AddNotification(title, message, status, autoDismissSeconds);
        }

        public void AddNotification(Func<string> title, Func<string> message, Pages.LabelStatus status, float autoDismissSeconds = 5)
        {
            NotificationPanel.AddNotification(title, message, status, autoDismissSeconds);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ClearFocus();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //ClearFocus();
        }

        private void ClearFocus()
        {
            //清除焦点
            UIElement focusedElement = Keyboard.FocusedElement as UIElement;

            if (focusedElement != null)
            {
                DependencyObject focusScope = FocusManager.GetFocusScope(focusedElement);
                FocusManager.SetFocusedElement(focusScope, null);
                Keyboard.ClearFocus();
            }
        }

        public bool SteamInit(bool debug = true)
        {
            try
            {
                Steamworks.SteamClient.Init(4907460);
                if (SteamClient.IsLoggedOn == false)
                {
                    if (debug)
                        AddNotification(
                            () => $"{LanguageManager.Inst.GetString("通知")}",
                            () => $"{LanguageManager.Inst.GetString("Steam 未连接")}",
                            Pages.LabelStatus.Error);
                }
                return SteamClient.IsLoggedOn;
            }
            catch (Exception ex)
            {
                if (debug)
                    AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("Steam 初始化失败")}: {ex.Message}",
                        Pages.LabelStatus.Error);
                return false;
            }
        }
    }
}
