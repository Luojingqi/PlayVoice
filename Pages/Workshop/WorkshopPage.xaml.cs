using PlayVoice.Pages.Loading;
using PlayVoice.Resources.Language;
using PlayVoice.Diagnostics;
using Steamworks;
using Steamworks.Ugc;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// WorkshopPage.xaml 的交互逻辑
    /// </summary>
    public partial class WorkshopPage : Page
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static WorkshopPage Inst { get; private set; }

        private LoadingAnimationPage loadingPage;
        public PageNumberSelection PageNumberSelection;

        public PageType NowPageType => (PageType)TopButtonListBox.SelectedIndex;
        public WorkshopPage()
        {
            Inst = this;
            InitializeComponent();
            TablePage.SetOwner(this);
            loadingPage = new();
            LoadingPageFrame.Content = loadingPage;

            PageNumberSelection = new();
            PageNumberSelectionFrame.Content = PageNumberSelection;

            TopButtonListBox.ItemsSource = new TabItem<WorkshopPage.PageType>[]
                {
                    new TabItem<PageType>(PageType.我的订阅),
                    new TabItem<PageType>(PageType.我的创作),
                    new TabItem<PageType>(PageType.创意工坊),
                    new TabItem<PageType>(PageType.好友创作),
                };
            TopButtonListBox.DisplayMemberPath = "Name";


            BottomButtonListBox.ItemsSource = tabItems;
            BottomButtonListBox.DisplayMemberPath = "Name";


            RankedComboBox.ItemsSource = new TabItem<RankedType>[]
                {
                    new TabItem<RankedType>(RankedType.热度),
                    new TabItem<RankedType>(RankedType.评分),
                    new TabItem<RankedType>(RankedType.最新),
                    new TabItem<RankedType>(RankedType.订阅),
                };
            RankedComboBox.DisplayMemberPath = "Name";

            var searchLanguageItems = CreateSearchLanguageItems();
            SearchLanguageListBox.ItemsSource = searchLanguageItems;
            isUpdatingSearchLanguageSelection = true;
            foreach (var item in searchLanguageItems)
                SearchLanguageListBox.SelectedItems.Add(item);
            UpdateSearchLanguageSelectionText();
            isUpdatingSearchLanguageSelection = false;


            ItemGroupListBox.SelectionChanged += (sender, e) =>
            {
                if (ItemGroupListBox.SelectedIndex == 1)
                {
                    ChangedPage(null, null);
                }
                ItemGroupListBox.SelectedIndex = -1;
            };

            detailPage = new ();
            DetailPageFrame.Content = detailPage;
            localDetailPage = new ();
            LocalDetailPageFrame.Content = localDetailPage;
            Unloaded += WorkshopPage_Unloaded;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TopButtonListBox.SelectedIndex = 0;
                foreach (var item in tabItems)
                    BottomButtonListBox.SelectedItems.Add(item);
                RankedComboBox.SelectedIndex = 0;

                TopButtonListBox.SelectionChanged += ChangedPage;
                BottomButtonListBox.SelectionChanged += ChangedPage;
                RankedComboBox.SelectionChanged += ChangedPage;

                ChangedPage(null, null);
            }, System.Windows.Threading.DispatcherPriority.Loaded);


        }
        public string SearchText => SearchTextBox.Text;
        public string[] SearchLanguages => SearchLanguageListBox.SelectedItems
            .Cast<SearchLanguageItem>()
            .Where(item => !string.IsNullOrWhiteSpace(item.SteamLanguage))
            .Select(item => item.SteamLanguage)
            .ToArray();

        private static List<SearchLanguageItem> CreateSearchLanguageItems()
        {
            var items = new List<SearchLanguageItem>();

            foreach (var language in SteamWorkshopLanguage.SupportedLanguages)
            {
                string displayName = CultureInfo
                    .GetCultureInfo(language.CultureName)
                    .DisplayName;
                items.Add(new SearchLanguageItem(displayName, language.SteamLanguage));
            }

            return items;
        }

        private bool isUpdatingSearchLanguageSelection;
        private bool hasSearchLanguageSelectionChanged;
        private void SearchLanguageListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (isUpdatingSearchLanguageSelection) return;

            isUpdatingSearchLanguageSelection = true;
            UpdateSearchLanguageSelectionText();
            isUpdatingSearchLanguageSelection = false;
            hasSearchLanguageSelectionChanged = true;
        }

        private void SearchLanguagePopup_Closed(object sender, EventArgs e)
        {
            if (!hasSearchLanguageSelectionChanged) return;

            hasSearchLanguageSelectionChanged = false;
            ChangedPage(null, null);
        }

        private void UpdateSearchLanguageSelectionText()
        {
            var selectedItems = SearchLanguageListBox.SelectedItems
                .Cast<SearchLanguageItem>()
                .ToArray();
            string text = selectedItems.Length switch
            {
                0 => LanguageManager.Inst.GetString("无"),
                var count when count == SearchLanguageListBox.Items.Count =>
                    LanguageManager.Inst.GetString("全部语言"),
                _ => string.Join(", ", selectedItems.Select(item => item.Name))
            };
            SearchLanguageDropDownButton.Content = text;
            SearchLanguageDropDownButton.ToolTip = text;
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;
            ChangedPage(null, null);
        }

        private bool isLoading = false;
        private async void ChangedPage(object sender, SelectionChangedEventArgs e)
        {
            if (isLoading == true) return;
            isLoading = true;
            CloseDetailPage();
            CloseLocalDetailPage();
            if (TopButtonListBox.SelectedIndex != (int)PageType.创意工坊)
            {
                BottomButtonListBox.Visibility = Visibility.Collapsed;
                UpperRightInputField.Visibility = Visibility.Collapsed;
                SearchLanguageInputField.Visibility = Visibility.Collapsed;
            }
            loadingPage.Visibility = Visibility.Visible;
            this.IsEnabled = false;
            await TablePage.SwitchPage(NowPageType);

            if (TopButtonListBox.SelectedIndex == (int)PageType.创意工坊)
            {
                BottomButtonListBox.Visibility = Visibility.Visible;
                UpperRightInputField.Visibility = Visibility.Visible;
                SearchLanguageInputField.Visibility = Visibility.Visible;
            }

            isLoading = false;
            this.IsEnabled = true;
            loadingPage.Visibility = Visibility.Hidden;
        }

        public enum PageType
        {
            我的订阅,
            我的创作,
            创意工坊,
            好友创作
        }

        private TabItem<TabType>[] tabItems = new[]
        {
            new TabItem<TabType>(TabType.音乐),
            new TabItem<TabType>(TabType.音效),
            new TabItem<TabType>(TabType.语音),
        };

        #region 下载图片
        public static async Task<BitmapImage> DownloadImageAsBitmapAsync(Item item, CancellationToken cancellationToken = default)
        {
            string imageUrl = item.PreviewImageUrl;
            return await DownloadImageAsBitmapAsync(imageUrl, cancellationToken);
        }
        private static async Task<BitmapImage> DownloadImageAsBitmapAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                byte[] imageData = await _httpClient.GetByteArrayAsync(url, cancellationToken);

                using (MemoryStream stream = new MemoryStream(imageData))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    return bitmap;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AppLog.Debug("创意工坊图片下载已取消。", "Workshop.ImageDownload");
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "创意工坊图片下载失败。", "Workshop.ImageDownload");
                return null;
            }
        }
        #endregion


        public static BitmapImage LoadImage(string path)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute); 
            bitmap.EndInit();
            return bitmap;
        }

        public enum TabType
        {
            音乐,
            音效,
            语音,
        }

        public static string TabTypeToSteam(TabType tabType)
        {
            switch (tabType)
            {
                case TabType.音乐:
                    return "music";
                case TabType.音效:
                    return "sound effect";
                case TabType.语音:
                    return "voice";
                default:
                    return string.Empty;
            }
        }

        public enum VisibleType
        {
            仅自己,
            仅好友,
            所有人,
        }

        public enum RankedType
        {
            热度,
            评分,
            最新,
            订阅
        }

        public class SearchLanguageItem
        {
            public string Name { get; }
            public string SteamLanguage { get; }

            public SearchLanguageItem(string name, string steamLanguage)
            {
                Name = name;
                SteamLanguage = steamLanguage;
            }
        }


        private TablePageItem nowSelectedItem = null;

        private DetailPage detailPage;
        public void OnClickTableItem_OpenDetailPage(TablePageItem tableItem)
        {
            CloseLocalDetailPage();
            if (!_isDetailPageOpen)
                ToggleDetailPage();

            if (tableItem == nowSelectedItem) return;
            if (nowSelectedItem != null)
            {
                nowSelectedItem.IsSelected = false;
            }
            nowSelectedItem = tableItem;
            nowSelectedItem.IsSelected = true;

            detailPage.SetTableItem(tableItem)
                .Forget("Workshop.Detail.Open");
        }

        public void CloseDetailPage()
        {
            if (_isDetailPageOpen)
                ToggleDetailPage();
        }

        private bool _isDetailPageOpen = false;
        public void ToggleDetailPage()
        {
            _isDetailPageOpen = !_isDetailPageOpen;

            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                To = _isDetailPageOpen ? 275 : 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            ThicknessAnimation marginAnimation = new ThicknessAnimation
            {
                To = _isDetailPageOpen ? new Thickness(5, 0, 0, 0) : new Thickness(0),
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            DetailPageBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
            DetailPageBorder.BeginAnimation(FrameworkElement.MarginProperty, marginAnimation);

            if (_isDetailPageOpen == false)
            {
                detailPage.Close();
                if(nowSelectedItem != null)
                {
                    nowSelectedItem.IsSelected = false;
                    nowSelectedItem = null;
                }
            }
        }


        private LocalDetailPage localDetailPage;
        public void OnClickTableItem_OpenLocalDetailPage(TablePageItem tableItem)
        {
            CloseDetailPage();
            if (!_isLocalDetailPageOpen)
                ToggleLocalDetailPage();

            if (tableItem == nowSelectedItem) return;
            if (nowSelectedItem != null)
            {
                nowSelectedItem.IsSelected = false;
            }
            nowSelectedItem = tableItem;
            nowSelectedItem.IsSelected = true;

            localDetailPage.SetTableItem(tableItem)
                .Forget("Workshop.LocalDetail.Open");
        }

        public void CloseLocalDetailPage()
        {
            if (_isLocalDetailPageOpen)
            {
                ToggleLocalDetailPage();
                return;
            }

            localDetailPage?.Close();
        }

        private void WorkshopPage_Unloaded(object sender, RoutedEventArgs e) =>
            CloseLocalDetailPage();

        private bool _isLocalDetailPageOpen = false;
        public void ToggleLocalDetailPage()
        {
            _isLocalDetailPageOpen = !_isLocalDetailPageOpen;

            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                To = _isLocalDetailPageOpen ? 600 : 0,
                Duration = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            ThicknessAnimation marginAnimation = new ThicknessAnimation
            {
                To = _isLocalDetailPageOpen ? new Thickness(5, 0, 0, 0) : new Thickness(0),
                Duration = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            LocalDetailPageBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
            LocalDetailPageBorder.BeginAnimation(FrameworkElement.MarginProperty, marginAnimation);

            if (_isLocalDetailPageOpen == false)
            {
                localDetailPage.Close();
                if (nowSelectedItem != null)
                {
                    nowSelectedItem.IsSelected = false;
                    nowSelectedItem = null;
                }
            }
        }


        public class TabItem<T> where T : Enum
        {
            public string Name { get; set; }
            public T Type { get; set; }
            public TabItem(T item)
            {
                Type = item;
                Name = LanguageManager.Inst.GetString(item.ToString());
            }
        }
    }
}
