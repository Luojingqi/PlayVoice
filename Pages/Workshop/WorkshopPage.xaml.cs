using PlayVoice.Pages.Loading;
using PlayVoice.Resources.Language;
using Steamworks;
using Steamworks.Ugc;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
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
        public string SearchText { get; set; }

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
            }
            loadingPage.Visibility = Visibility.Visible;
            this.IsEnabled = false;
            await TablePage.SwitchPage(NowPageType);

            if (TopButtonListBox.SelectedIndex == (int)PageType.创意工坊)
            {
                BottomButtonListBox.Visibility = Visibility.Visible;
                UpperRightInputField.Visibility = Visibility.Visible;
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
        public static async Task<BitmapImage> DownloadImageAsBitmapAsync(Item item)
        {
            string imageUrl = item.PreviewImageUrl;
            return await DownloadImageAsBitmapAsync(imageUrl);
        }
        private static async Task<BitmapImage> DownloadImageAsBitmapAsync(string url)
        {
            try
            {
                byte[] imageData = await _httpClient.GetByteArrayAsync(url);

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
            catch (Exception ex)
            {
                Console.WriteLine($"图片下载失败: {ex.Message}");
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

            detailPage.SetTableItem(tableItem);
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
                To = _isDetailPageOpen ? 225 : 0,
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

            localDetailPage.SetTableItem(tableItem);
        }

        public void CloseLocalDetailPage()
        {
            if (_isLocalDetailPageOpen)
                ToggleLocalDetailPage();
        }

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
