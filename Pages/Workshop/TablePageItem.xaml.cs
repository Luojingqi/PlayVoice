using PlayVoice.Resources.Language;
using Steamworks.Ugc;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// TablePageItem.xaml 的交互逻辑
    /// </summary>
    public partial class TablePageItem : UserControl
    {


        public TablePageItem(Item? item, string localItemPath)
        {
            InitializeComponent();
            this.Item = item;
            this.LocalItemPath = localItemPath;
            this.IsEnabled = false;
        }

        public async override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
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

                    #endregion
                }
            }
            else
            {
                var item = Item.Value;
                if (item.IsSubscribed && item.IsInstalled == false)
                {
                    #region 订阅了没下载
                    _LoadingBar.Visibility = Visibility.Visible;
                    ItemTitle = item.Title;
                    var getImageTask = WorkshopPage.DownloadImageAsBitmapAsync(item);
                    if (item.IsDownloadPending)
                    {
                        Console.Write("等待下载");
                    }
                    else if (item.IsDownloading)
                    {
                        Console.Write("下载ing");
                        TablePage.NowDownloadingItemSet.Add(this);
                    }
                    else
                    {
                        item.Download(false);
                        MainWindow.Inst.AddNotification(
                           () => $"{LanguageManager.Inst.GetString("通知")}",
                           () => $"{LanguageManager.Inst.GetString("开始下载")} : {item.Title}",
                           LabelStatus.Warning, 3.5f);
                    }

                    #endregion
                }
                else
                {
                    #region 直接访问steam
                    _LoadingBar.Visibility = Visibility.Visible;
                    ItemTitle = item.Title;
                    _LoadingBar.SetProgress(0.2);
                    var getImageTask = WorkshopPage.DownloadImageAsBitmapAsync(item);
                    for (int i = 0; i < 3; i++)
                    {
                        if (getImageTask.IsCompleted) break;
                        await Task.Delay(250);
                        _LoadingBar.SetProgress(0.4 + 0.1 * i);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        if (getImageTask.IsCompleted) break;
                        await Task.Delay(125);
                        _LoadingBar.SetProgress(0.75 + 0.01 * i);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        if (getImageTask.IsCompleted) break;
                        await Task.Delay(300);
                        _LoadingBar.SetProgress(0.91 + 0.001 * i);
                    }
                    if (getImageTask.IsCompleted)
                    {
                        ItemImage = await getImageTask;
                        _LoadingBar.SetProgress(1, 100);
                        await Task.Delay(100);
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
        public static readonly DependencyProperty ItemTitleProperty =
            DependencyProperty.Register("ItemTitle", typeof(string), typeof(TablePageItem), new PropertyMetadata("未命名"));
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
