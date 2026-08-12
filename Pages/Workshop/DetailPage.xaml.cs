using PlayVoice.Resources.Language;
using PlayVoice.Audio;
using Steamworks;
using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// DetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class DetailPage : UserControl
    {
        private bool? userVote;
        private bool isFavorite;
        private CancellationTokenSource _loadCancellation;
        private int _loadGeneration;

        public DetailPage()
        {
            InitializeComponent();

            AudioListBox.ItemsSource = DetailItemList;


            FoldUpButton.SelectionChanged += (s, e) =>
            {
                if (FoldUpButton.SelectedIndex == -1) return;
                WorkshopPage.Inst.CloseDetailPage();
                FoldUpButton.SelectedIndex = -1;
            };

            SubscribeButton.Visibility = Visibility.Collapsed;
            UnSubscribeButton.Visibility = Visibility.Collapsed;

            SubscribeButton.SelectionChanged += async (s, e) =>
            {
                if (SubscribeButton.SelectedIndex == -1) return;
                var item = tableItem.Item.Value;
                MainWindow.Inst.AddNotification(
                       () => $"{LanguageManager.Inst.GetString("通知")}",
                       () => $"{LanguageManager.Inst.GetString("正在订阅")} : {item.Title}",
                       LabelStatus.Warning, 3.5f);
                if (await item.Subscribe())
                {
                    MainWindow.Inst.AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("已订阅")} : {item.Title}",
                        LabelStatus.Warning, 3.5f);
                    await WorkshopPage.Inst.TablePage.ReLoadItem(tableItem);
                }
                else
                {
                    MainWindow.Inst.AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("订阅失败")} : {item.Title}",
                        LabelStatus.Warning, 3.5f);
                }
                CheckSubscribe();
                SubscribeButton.SelectedIndex = -1;
            };
            UnSubscribeButton.SelectionChanged += async (s, e) =>
            {
                if (UnSubscribeButton.SelectedIndex == -1) return;
                var item = tableItem.Item.Value;
                MainWindow.Inst.AddNotification(
                       () => $"{LanguageManager.Inst.GetString("通知")}",
                       () => $"{LanguageManager.Inst.GetString("正在退订")} : {item.Title}",
                       LabelStatus.Warning, 3.5f);
                if (await item.Unsubscribe())
                {
                    MainWindow.Inst.AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("已退订")} : {item.Title}",
                        LabelStatus.Warning, 3.5f);
                    WorkshopPage.Inst.TablePage.ReLoadItem(tableItem);
                }
                else
                {
                    MainWindow.Inst.AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("退订失败")} : {item.Title}",
                        LabelStatus.Warning, 3.5f);
                }
                CheckSubscribe();
                UnSubscribeButton.SelectedIndex = -1;
            };

            FeedbackButton.SelectionChanged += async (s, e) =>
            {
                int selectedIndex = FeedbackButton.SelectedIndex;
                if (selectedIndex == -1 || tableItem == null) return;

                var item = tableItem.Item.Value;
                string action = selectedIndex switch
                {
                    0 => LanguageManager.Inst.GetString("好评"),
                    1 => LanguageManager.Inst.GetString("差评"),
                    _ => LanguageManager.Inst.GetString("喜欢")
                };

                bool success = selectedIndex switch
                {
                    0 => await item.Vote(true) == Result.OK,
                    1 => await item.Vote(false) == Result.OK,
                    _ => isFavorite ? await item.RemoveFavorite() : await item.AddFavorite()
                };

                if (success)
                {
                    if (selectedIndex == 0) userVote = true;
                    else if (selectedIndex == 1) userVote = false;
                    else isFavorite = !isFavorite;
                    UpdateFeedbackState();
                }

                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => success
                        ? $"{action} : {item.Title}"
                        : $"{action} {LanguageManager.Inst.GetString("失败")} : {item.Title}",
                    success ? LabelStatus.Success : LabelStatus.Error, 3.5f);

                FeedbackButton.SelectedIndex = -1;
            };
        }



        private ObservableCollection<DetailPageItem> DetailItemList = new();

        private TablePageItem tableItem;
        public async Task SetTableItem(TablePageItem tableItem)
        {
            Close();
            this.tableItem = tableItem;
            int generation = _loadGeneration;
            _loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _loadCancellation.Token;
            var item = tableItem.Item.Value;
            ItemTitle.Text = item.Title;
            SteamScoreText.Text = $"★ {item.Score * 5:0.0}";
            ImageLoadingPanel.Visibility = Visibility.Visible;
            MetadataLoadingPanel.Visibility = Visibility.Visible;
            ImageLoadingBar.SetProgressInstant(0.08);
            MetadataLoadingBar.SetProgressInstant(0.08);

            try
            {
                var imageTask = LoadImageAsync(item, generation, cancellationToken);
                var authorTask = LoadAuthor(item.Owner.Id, generation, cancellationToken);

                var metaData = JsonTool.ToObject<ResourceDataConfig.Metadata>(item.Metadata);
                var metadataItems = metaData?.ItemList ?? new List<ResourceDataConfig.ResourceItem>();
                for (int i = 0; i < metadataItems.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsCurrentLoad(generation)) return;

                    DetailItemList.Add(new DetailPageItem(metadataItems[i]));
                    MetadataLoadingBar.SetProgressInstant(0.12 + 0.58 * (i + 1) / Math.Max(1, metadataItems.Count));
                    await Task.Delay(16, cancellationToken);
                }

                if (metaData != null && IsCurrentLoad(generation))
                {
                    long totalSize = metaData.Size > 0
                        ? metaData.Size
                        : metadataItems.Aggregate(
                            0L,
                            (total, metadataItem) => checked(total + metadataItem.Size));
                    ResourceSizeText.Text = AudioData.SizeToString(totalSize);
                    ResourceSizeText.Visibility = Visibility.Visible;
                }

                MetadataLoadingBar.SetProgress(0.78, 180);
                await CheckSubscribe(tableItem);
                if (!IsCurrentLoad(generation)) return;

                MetadataLoadingBar.SetProgress(0.9, 180);
                var vote = await item.GetUserVote();
                if (!IsCurrentLoad(generation)) return;

                userVote = vote?.VotedUp == true ? true : vote?.VotedDown == true ? false : null;
                UpdateFeedbackState();
                MetadataLoadingBar.SetProgress(1, 120);
                await Task.Delay(100, cancellationToken);
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    ResourceDescriptionText.Text = item.Description;
                    DescriptionRow.Height = new GridLength(1, GridUnitType.Star);
                    ResourceDescriptionPanel.Visibility = Visibility.Visible;
                }
                MetadataLoadingPanel.Visibility = Visibility.Collapsed;

                await imageTask;
                await authorTask;
                if (!IsCurrentLoad(generation)) return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载创意工坊详情失败: {ex.Message}");
                if (IsCurrentLoad(generation))
                {
                    ImageLoadingPanel.Visibility = Visibility.Collapsed;
                    MetadataLoadingPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void Close()
        {
            _loadGeneration++;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            tableItem = null;
            DetailItemList.Clear();
            BgImage.Source = null;
            SubscribeButton.Visibility = Visibility.Collapsed;
            UnSubscribeButton.Visibility = Visibility.Collapsed;
            FeedbackButton.SelectedIndex = -1;
            AuthorPanel.Visibility = Visibility.Hidden;
            AuthorAvatar.Source = null;
            AuthorName.Text = string.Empty;
            ResourceDescriptionText.Text = string.Empty;
            ResourceDescriptionPanel.Visibility = Visibility.Collapsed;
            DescriptionRow.Height = new GridLength(0);
            ResourceSizeText.Text = string.Empty;
            ResourceSizeText.Visibility = Visibility.Collapsed;
            SteamScoreText.Text = "★ --";
            userVote = null;
            isFavorite = false;
            UpdateFeedbackState();
            ImageLoadingPanel.Visibility = Visibility.Collapsed;
            MetadataLoadingPanel.Visibility = Visibility.Collapsed;
        }

        private async Task LoadAuthor(SteamId owner, int generation, CancellationToken cancellationToken)
        {
            var author = await WorkshopAuthorInfo.LoadAsync(owner);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentLoad(generation) || tableItem.Item.Value.Owner.Id != owner) return;

            AuthorName.Text = author.Name;
            AuthorAvatar.Source = author.Avatar;
            AuthorPanel.Visibility = Visibility.Visible;
        }

        private bool IsCurrentLoad(int generation) =>
            generation == _loadGeneration && tableItem?.Item.HasValue == true;

        private async Task LoadImageAsync(Item item, int generation, CancellationToken cancellationToken)
        {
            try
            {
                var imageTask = WorkshopPage.DownloadImageAsBitmapAsync(item, cancellationToken);
                await SimulateProgressAsync(imageTask, ImageLoadingBar, 0.88, cancellationToken);
                var image = await imageTask;
                if (!IsCurrentLoad(generation)) return;

                BgImage.Source = image;
                ImageLoadingBar.SetProgress(1, 120);
                await Task.Delay(100, cancellationToken);
                if (IsCurrentLoad(generation))
                    ImageLoadingPanel.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载创意工坊图片失败: {ex.Message}");
                if (IsCurrentLoad(generation))
                    ImageLoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private static async Task SimulateProgressAsync(
            Task task,
            CircularProgressBar progressBar,
            double ceiling,
            CancellationToken cancellationToken)
        {
            double progress = 0.08;
            while (!task.IsCompleted)
            {
                await Task.Delay(120, cancellationToken);
                progress += Math.Max(0.006, (ceiling - progress) * 0.12);
                progressBar.SetProgress(Math.Min(progress, ceiling), 180);
            }
        }

        private void AuthorName_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (tableItem?.Item.HasValue == true)
                SteamFriends.OpenUserOverlay(tableItem.Item.Value.Owner.Id, "steamid");
        }

        private void OpenWorkshopPage_Click(object sender, RoutedEventArgs e)
        {
            if (tableItem?.Item.HasValue == true)
                SteamFriends.OpenWebOverlay(tableItem.Item.Value.Url);
        }

        private void CopyWorkshopUrl_Click(object sender, RoutedEventArgs e)
        {
            if (tableItem?.Item.HasValue != true) return;
            Clipboard.SetText(tableItem.Item.Value.Url);
            MainWindow.Inst.AddNotification(
                () => LanguageManager.Inst.GetString("通知"),
                () => LanguageManager.Inst.GetString("已复制") + "URL",
                LabelStatus.Success, 3.5f);
        }

        private void ReportWorkshopItem_Click(object sender, RoutedEventArgs e)
        {
            if (tableItem?.Item.HasValue == true)
                SteamFriends.OpenWebOverlay(tableItem.Item.Value.Url);
        }

        private void UpdateFeedbackState()
        {
            SetFeedbackState(UpvoteIcon, userVote == true, "Success");
            SetFeedbackState(DownvoteIcon, userVote == false, "Error");
            SetFeedbackState(FavoriteIcon, isFavorite, "Warning");
        }

        private void SetFeedbackState(System.Windows.Shapes.Path icon, bool active, string brushKey)
        {
            icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, active ? brushKey : "AccentColor");
        }

        private async Task<bool> CheckSubscribe(TablePageItem expectedItem = null)
        {
            expectedItem ??= tableItem;
            if (expectedItem?.Item.HasValue != true)
            {
                SubscribeButton.Visibility = Visibility.Collapsed;
                UnSubscribeButton.Visibility = Visibility.Collapsed;
                return false;
            }
            var item = await SteamUGC.QueryFileAsync(expectedItem.Item.Value.Id);
            if (!ReferenceEquals(tableItem, expectedItem)) return false;
            if (item.HasValue)
            {
                bool ret = item.Value.IsSubscribed;
                if (ret == true)
                {
                    SubscribeButton.Visibility = Visibility.Collapsed;
                    UnSubscribeButton.Visibility = Visibility.Visible;
                }
                else
                {
                    SubscribeButton.Visibility = Visibility.Visible;
                    UnSubscribeButton.Visibility = Visibility.Collapsed;
                }
                return ret;
            }
            else
            {
                SubscribeButton.Visibility = Visibility.Collapsed;
                UnSubscribeButton.Visibility = Visibility.Collapsed;
                return false;
            }
        }

    }
}
