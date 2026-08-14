using PlayVoice.Pages.Sidebar;
using PlayVoice.Pages.Setting;
using PlayVoice.Resources.Language;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Themes;
using Steamworks;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
        private bool isBodyInteractionBlocked;
        private readonly Dictionary<string, StartupRequirementVisual> startupRequirementVisuals = new();
        private readonly HashSet<string> desiredStartupRequirementIds = new();
        private int startupChecklistRefreshVersion;

        private sealed class StartupRequirementVisual
        {
            public Border Container { get; init; }
            public TextBlock Text { get; init; }
            public bool IsExiting { get; set; }
        }

        public MainWindow()
        {
            Inst = this;
            new LanguageManager();
            ThemeManager.Init();
            viewModel = new MainViewModel();
            DataContext = viewModel;
            new GlobalData();

            InitializeComponent();
            GlobalData.Inst.StartupRequirementsChanged += RefreshStartupRequirements;
            RefreshStartupRequirements();
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
            LanguageManager.Inst.CultureChanged += (culture, language) =>
            {
                UpdateTrayLanguage();
                RefreshStartupRequirements();
            };
            Loaded += MainWindow_OnLoaded;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_OnLoaded;

            if (!GlobalData.Inst.Config.HasAcknowledgedTutorialPrompt)
            {
                var tutorialPromptWindow = new TutorialPromptWindow
                {
                    Owner = this
                };

                if (tutorialPromptWindow.ShowDialog() == true)
                {
                    GlobalData.Inst.Config.HasAcknowledgedTutorialPrompt = true;
                    GlobalData.Inst.Config.Save();
                }
            }

            GlobalData.Inst.EnableAutomaticRun();
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
            if (!isSystemSessionEnding
                && !GlobalData.Inst.Config.HasSelectedCloseBehavior)
            {
                e.Cancel = true;
                var selectionWindow = new CloseBehaviorSelectionWindow
                {
                    Owner = this
                };
                if (selectionWindow.ShowDialog() != true
                    || !selectionWindow.SelectedBehavior.HasValue)
                    return;

                bool minimizeToTray = selectionWindow.SelectedBehavior ==
                    CloseBehaviorSelectionWindow.CloseBehavior.MinimizeToTray;
                GlobalData.Inst.Config.MinimizeToTrayOnClose = minimizeToTray;
                GlobalData.Inst.Config.HasSelectedCloseBehavior = true;
                GlobalData.Inst.Config.Save();

                if (minimizeToTray)
                    MinimizeToTray();
                else
                    ExitApplication();
                return;
            }

            if (!isSystemSessionEnding
                && GlobalData.Inst.Config.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                MinimizeToTray();
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

        private void MinimizeToTray()
        {
            ShowInTaskbar = false;
            Hide();
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
            presetTrayMenuItem.Text = LanguageManager.Inst.GetString("音频预设");
            exitTrayMenuItem.Text = LanguageManager.Inst.GetString("退出");
        }

        private void RefreshStartupRequirements()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(RefreshStartupRequirements);
                return;
            }

            IReadOnlyList<StartupRequirement> requirements =
                GlobalData.Inst.GetIncompleteStartupRequirements();
            int refreshVersion = ++startupChecklistRefreshVersion;

            desiredStartupRequirementIds.Clear();
            foreach (StartupRequirement requirement in requirements)
                desiredStartupRequirementIds.Add(requirement.Id);

            if (requirements.Count > 0)
                AnimateStartupChecklistIn();

            for (int index = 0; index < requirements.Count; index++)
            {
                StartupRequirement requirement = requirements[index];
                if (startupRequirementVisuals.TryGetValue(
                    requirement.Id, out StartupRequirementVisual visual))
                {
                    visual.Text.Text = requirement.Message;
                    if (visual.IsExiting)
                    {
                        visual.IsExiting = false;
                        AnimateRequirementIn(visual);
                    }
                    continue;
                }

                visual = CreateStartupRequirementVisual(requirement);
                startupRequirementVisuals.Add(requirement.Id, visual);
                StartupRequirementsPanel.Children.Insert(
                    Math.Min(index, StartupRequirementsPanel.Children.Count),
                    visual.Container);
                AnimateRequirementIn(visual);
            }

            foreach (var item in startupRequirementVisuals.ToArray())
            {
                if (desiredStartupRequirementIds.Contains(item.Key)
                    || item.Value.IsExiting)
                    continue;

                item.Value.IsExiting = true;
                AnimateRequirementOut(item.Key, item.Value);
            }

            if (requirements.Count == 0)
                _ = HideStartupChecklistAfterItemsAsync(refreshVersion);
        }

        private StartupRequirementVisual CreateStartupRequirementVisual(
            StartupRequirement requirement)
        {
            var container = new Border
            {
                Margin = new Thickness(0, 3, 0, 3),
                Opacity = 0,
                RenderTransform = new TranslateTransform(24, 0)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var marker = new Ellipse
            {
                Width = 5,
                Height = 5,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            marker.SetResourceReference(Shape.FillProperty, "AccentColor");

            var text = new TextBlock
            {
                Text = requirement.Message,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            text.SetResourceReference(TextBlock.FontFamilyProperty, "FontLight");

            Grid.SetColumn(marker, 0);
            Grid.SetColumn(text, 1);
            grid.Children.Add(marker);
            grid.Children.Add(text);
            container.Child = grid;

            return new StartupRequirementVisual
            {
                Container = container,
                Text = text
            };
        }

        private static void AnimateRequirementIn(StartupRequirementVisual visual)
        {
            AnimateSlide(
                visual.Container,
                (TranslateTransform)visual.Container.RenderTransform,
                1,
                0,
                null);
        }

        private void AnimateRequirementOut(
            string id,
            StartupRequirementVisual visual)
        {
            AnimateSlide(
                visual.Container,
                (TranslateTransform)visual.Container.RenderTransform,
                0,
                24,
                () =>
                {
                    if (desiredStartupRequirementIds.Contains(id)) return;
                    StartupRequirementsPanel.Children.Remove(visual.Container);
                    startupRequirementVisuals.Remove(id);
                });
        }

        private void AnimateStartupChecklistIn()
        {
            var transform = (TranslateTransform)StartupChecklistBorder.RenderTransform;
            if (StartupChecklistBorder.Visibility != Visibility.Visible)
            {
                StartupChecklistBorder.Opacity = 0;
                transform.X = 24;
                StartupChecklistBorder.Visibility = Visibility.Visible;
            }
            AnimateSlide(StartupChecklistBorder, transform, 1, 0, null, 260);
        }

        private async Task HideStartupChecklistAfterItemsAsync(int refreshVersion)
        {
            await Task.Delay(230);
            if (refreshVersion != startupChecklistRefreshVersion
                || desiredStartupRequirementIds.Count > 0)
                return;

            var transform = (TranslateTransform)StartupChecklistBorder.RenderTransform;
            AnimateSlide(
                StartupChecklistBorder,
                transform,
                0,
                24,
                () =>
                {
                    if (desiredStartupRequirementIds.Count == 0)
                        StartupChecklistBorder.Visibility = Visibility.Collapsed;
                },
                260);
        }

        private static void AnimateSlide(
            UIElement element,
            TranslateTransform transform,
            double targetOpacity,
            double targetX,
            Action completed,
            int durationMilliseconds = 220)
        {
            var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var opacityAnimation = new DoubleAnimation
            {
                From = element.Opacity,
                To = targetOpacity,
                Duration = duration,
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd
            };
            var translateAnimation = new DoubleAnimation
            {
                From = transform.X,
                To = targetX,
                Duration = duration,
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd
            };
            opacityAnimation.Completed += (sender, args) =>
            {
                element.BeginAnimation(OpacityProperty, null);
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                element.Opacity = targetOpacity;
                transform.X = targetX;
                completed?.Invoke();
            };
            element.BeginAnimation(OpacityProperty, opacityAnimation);
            transform.BeginAnimation(TranslateTransform.XProperty, translateAnimation);
        }

        private void RefreshTrayPresetMenu()
        {
            presetTrayMenuItem.DropDownItems.Clear();
            string currentPresetName = GlobalData.Inst.ActiveAudioPreset?.Config?.Name;

            var noPresetItem = new System.Windows.Forms.ToolStripMenuItem(LanguageManager.Inst.GetString("无"))
            {
                Checked = GlobalData.Inst.ActiveAudioPreset == null
            };
            noPresetItem.Click += async (s, e) => await SwitchPresetFromTrayAsync(null);
            presetTrayMenuItem.DropDownItems.Add(noPresetItem);
            presetTrayMenuItem.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());

            try
            {
                foreach (string presetName in AudioPresetDataTool.GetAllAudioPresetName().OrderBy(name => name))
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
                // 音频预设目录尚未创建时仅显示“无”。
            }
        }

        private async Task SwitchPresetFromTrayAsync(string presetName)
        {
            if (isSwitchingPreset) return;
            isSwitchingPreset = true;
            presetTrayMenuItem.Enabled = false;

            try
            {
                if (presetName == null)
                {
                    GlobalData.Inst.ActiveAudioPreset = null;
                    return;
                }

                if (string.Equals(GlobalData.Inst.ActiveAudioPreset?.Config?.Name, presetName, StringComparison.OrdinalIgnoreCase))
                    return;

                var presetData = await AudioPresetDataTool.LoadAudioPresetData(presetName);
                if (presetData != null)
                    GlobalData.Inst.ActiveAudioPreset = presetData;
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

        public void SetBodyInteractionBlocked(bool isBlocked)
        {
            isBodyInteractionBlocked = isBlocked;
            UpdateBodyInteractionBlockerVisibility();
        }

        public void ShowBodyInteractionOverlay(UIElement overlay)
        {
            BodyInteractionOverlayHost.Content = overlay;
            UpdateBodyInteractionBlockerVisibility();
        }

        public void HideBodyInteractionOverlay(UIElement overlay)
        {
            if (!ReferenceEquals(BodyInteractionOverlayHost.Content, overlay))
                return;

            BodyInteractionOverlayHost.Content = null;
            UpdateBodyInteractionBlockerVisibility();
        }

        private void UpdateBodyInteractionBlockerVisibility()
        {
            BodyInteractionBlocker.Visibility = isBodyInteractionBlocked
                || BodyInteractionOverlayHost.Content != null
                ? Visibility.Visible
                : Visibility.Collapsed;
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
