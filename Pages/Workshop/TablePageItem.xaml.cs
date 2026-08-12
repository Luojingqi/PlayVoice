using PlayVoice.Resources.Language;
using Steamworks.Ugc;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// TablePageItem.xaml 的交互逻辑
    /// </summary>
    public partial class TablePageItem : UserControl
    {
        private static readonly object DownloadRequestLock = new();
        private static readonly Dictionary<Steamworks.Data.PublishedFileId, DateTime> DownloadRequests = new();
        private static readonly TimeSpan DownloadRequestLifetime = TimeSpan.FromMinutes(10);

        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private bool _isTrackingDownload;
        private bool _templateApplied;

        public TablePageItem(Item? item, string localItemPath)
        {
            InitializeComponent();
            ItemTitle = LanguageManager.Inst.GetString("未命名");
            this.Item = item;
            this.LocalItemPath = localItemPath;
            this.IsEnabled = false;
            Unloaded += TablePageItem_Unloaded;
        }

        public async override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (_templateApplied) return;
            _templateApplied = true;

            _LoadingBar = GetTemplateChild("LoadingBar") as CircularProgressBar;
            _LoadingBar.Visibility = Visibility.Hidden;
            LoadFailedText = GetTemplateChild("LoadFailedText") as TextBlock;
            this.IsEnabled = true;

            if (string.IsNullOrEmpty(LocalItemPath) == false)
            {

                var localItemPath = LocalItemPath;
                if (JsonTool.LoadJson<ResourceDataConfig>(Path.Combine(localItemPath, "ResourceConfig.json"), out var resourceData))
                {
                    ItemTitle = resourceData.Title;
                    ItemImage = WorkshopPage.LoadImage(Path.Combine(localItemPath, $"Thumbnail{resourceData.ThumbnailFormat}"));
                }
                else
                {
                    LoadFailedText.Visibility = Visibility.Visible;
                    LoadFailedText.Text = LanguageManager.Inst.GetString("物品加载失败");
                }

                if (Item.HasValue == false)
                {
                    #region 无网络连接

                    #endregion
                }
                else
                {
                    #region 连接到steam
                    if (Item.Value.IsSubscribed && Item.Value.NeedsUpdate)
                        UpdateDownloadProgress();
                    #endregion
                }
            }
            else
            {
                var item = Item.Value;
                if (item.IsSubscribed && (!item.IsInstalled || item.NeedsUpdate))
                {
                    #region 订阅了没下载
                    ItemTitle = item.Title;
                    UpdateDownloadProgress();

                    #endregion
                }
                else
                {
                    #region 直接访问steam
                    _LoadingBar.Visibility = Visibility.Visible;
                    ItemTitle = item.Title;
                    _LoadingBar.SetProgress(0.2);
                    var image = await WorkshopPage.DownloadImageAsBitmapAsync(item, _lifetimeCancellation.Token);
                    if (_lifetimeCancellation.IsCancellationRequested) return;

                    if (image != null)
                    {
                        ItemImage = image;
                        _LoadingBar.SetProgress(1, 100);
                        await Task.Delay(100);
                        if (_lifetimeCancellation.IsCancellationRequested) return;
                        _LoadingBar.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        LoadFailedText.Visibility = Visibility.Visible;
                        LoadFailedText.Text = LanguageManager.Inst.GetString("图片加载失败");
                        _LoadingBar.Visibility = Visibility.Hidden;
                    }
                    #endregion
                }
            }


            if (Item.HasValue == false)
            {
                
            }
            else
            {
                
            }
        }

        /// <summary>
        /// 从 Steam 原生状态读取实时下载进度。返回 true 表示刚刚完成，需要刷新卡片。
        /// </summary>
        public bool UpdateDownloadProgress()
        {
            if (!Item.HasValue) return false;

            var item = Item.Value;
            bool needsDownload = item.IsSubscribed && (!item.IsInstalled || item.NeedsUpdate);
            if (!needsDownload)
            {
                ForgetDownloadRequest(item.Id);
                if (!_isTrackingDownload) return false;

                _isTrackingDownload = false;
                _LoadingBar?.SetProgress(1, 100);
                return item.IsInstalled;
            }

            _isTrackingDownload = true;
            ItemTitle = item.Title;
            if (_LoadingBar != null)
            {
                _LoadingBar.Visibility = Visibility.Visible;

                long totalBytes = item.DownloadBytesTotal;
                long downloadedBytes = item.DownloadBytesDownloaded;
                if (item.IsDownloading && totalBytes > 0 && downloadedBytes >= 0)
                {
                    double progress = (double)downloadedBytes / totalBytes;
                    _LoadingBar.SetProgressInstant(progress);
                }
                else
                {
                    // Steam 尚未返回有效字节信息时不能使用 DownloadAmount，部分状态下它会是占位值 1。
                    _LoadingBar.SetWaiting(LanguageManager.Inst.GetString("等待下载"));
                }
            }

            // 请求登记按物品 ID 跨卡片生效，快速切页和重复加载不会再次调用 Download。
            if (!item.IsDownloading && !item.IsDownloadPending && TryRegisterDownloadRequest(item.Id))
            {
                if (item.Download(false))
                {
                    MainWindow.Inst.AddNotification(
                       () => $"{LanguageManager.Inst.GetString("通知")}",
                       () => $"{LanguageManager.Inst.GetString("开始下载")} : {item.Title}",
                       LabelStatus.Warning, 3.5f);
                }
                else
                {
                    DeferDownloadRequest(item.Id, TimeSpan.FromSeconds(2));
                }
            }

            return false;
        }

        private static bool TryRegisterDownloadRequest(Steamworks.Data.PublishedFileId itemId)
        {
            lock (DownloadRequestLock)
            {
                DateTime now = DateTime.UtcNow;
                foreach (var expiredId in DownloadRequests
                             .Where(pair => pair.Value <= now)
                             .Select(pair => pair.Key)
                             .ToArray())
                {
                    DownloadRequests.Remove(expiredId);
                }

                if (DownloadRequests.ContainsKey(itemId)) return false;

                DownloadRequests[itemId] = now.Add(DownloadRequestLifetime);
                return true;
            }
        }

        private static void ForgetDownloadRequest(Steamworks.Data.PublishedFileId itemId)
        {
            lock (DownloadRequestLock)
            {
                DownloadRequests.Remove(itemId);
            }
        }

        private static void DeferDownloadRequest(Steamworks.Data.PublishedFileId itemId, TimeSpan delay)
        {
            lock (DownloadRequestLock)
            {
                DownloadRequests[itemId] = DateTime.UtcNow.Add(delay);
            }
        }

        private void TablePageItem_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= TablePageItem_Unloaded;
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _LoadingBar?.BeginAnimation(CircularProgressBar.ProgressValueProperty, null);
        }
        public static readonly DependencyProperty ItemTitleProperty =
            DependencyProperty.Register("ItemTitle", typeof(string), typeof(TablePageItem), new PropertyMetadata(string.Empty));
        public string ItemTitle
        {
            get { return (string)GetValue(ItemTitleProperty); }
            set { SetValue(ItemTitleProperty, value); }
        }

        public static readonly DependencyProperty ItemImageProperty =
            DependencyProperty.Register("ItemImage", typeof(ImageSource), typeof(TablePageItem), new PropertyMetadata(null));
        public ImageSource ItemImage
        {
            get { return (ImageSource)GetValue(ItemImageProperty); }
            set { SetValue(ItemImageProperty, value); }
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(TablePageItem), new PropertyMetadata(false));
        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }


        private CircularProgressBar _LoadingBar;
        public CircularProgressBar LoadingBar => _LoadingBar;

        private TextBlock LoadFailedText;

        public Item? Item;
        public string LocalItemPath;

        public event Action<TablePageItem> OnClick;

        private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OnClick?.Invoke(this);
        }
    }
}
