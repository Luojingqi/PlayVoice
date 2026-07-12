using PlayVoice.Pages.Sidebar;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using Steamworks;
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
            Process.GetCurrentProcess().Kill();
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