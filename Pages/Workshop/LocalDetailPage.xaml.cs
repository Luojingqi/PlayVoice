using NAudio.Wave;
using PlayVoice.Audio;
using PlayVoice.Diagnostics;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using Steamworks;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// LocalDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class LocalDetailPage : UserControl
    {
        private bool? userVote;
        private bool isFavorite;
        private CancellationTokenSource _loadCancellation;
        private int _loadGeneration;

        public LocalDetailPage()
        {
            InitializeComponent();
            this.DataContext = this;

            AudioListBox.ItemsSource = LocalDetailItemList;


            FoldUpButton.SelectionChanged += (s, e) =>
            {
                if (FoldUpButton.SelectedIndex == -1) return;
                WorkshopPage.Inst.CloseLocalDetailPage();
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
                await CheckSubscribe();
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
                    await WorkshopPage.Inst.TablePage.ReLoadItem(tableItem);
                }
                else
                {
                    MainWindow.Inst.AddNotification(
                        () => $"{LanguageManager.Inst.GetString("通知")}",
                        () => $"{LanguageManager.Inst.GetString("退订失败")} : {item.Title}",
                        LabelStatus.Warning, 3.5f);
                }
                await CheckSubscribe();
                UnSubscribeButton.SelectedIndex = -1;
            };

            FeedbackButton.SelectionChanged += async (s, e) =>
            {
                int selectedIndex = FeedbackButton.SelectedIndex;
                if (selectedIndex == -1 || tableItem?.Item.HasValue != true) return;

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
                    () => LanguageManager.Inst.GetString("通知"),
                    () => success
                        ? $"{action} : {item.Title}"
                        : $"{action} {LanguageManager.Inst.GetString("失败")} : {item.Title}",
                    success ? LabelStatus.Success : LabelStatus.Error, 3.5f);

                FeedbackButton.SelectedIndex = -1;
            };

            CheckBoxListBox.SelectionChanged += CheckBoxListBox_SelectionChanged;
        }

        private bool selectAll = false;
        private bool SelectAll
        {
            get => selectAll;
            set
            {
                selectAll = value;
                if (selectAll)
                    SelectCenterPoint.Visibility = Visibility.Visible;
                else
                    SelectCenterPoint.Visibility = Visibility.Collapsed;
            }
        }
        private void CheckBoxListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CheckBoxListBox.SelectedIndex == -1) return;

            switch (CheckBoxListBox.SelectedIndex)
            {
                case 0: SelectAllButton_Click(); break;
                case 1: CopyButton_Click(); break;
                case 2: FolderButton_Click(); break;
            }

            CheckBoxListBox.SelectedIndex = -1;
        }
        private void SelectAllButton_Click()
        {
            var b = !SelectAll;
            SelectAll = b;
            foreach (var item in LocalDetailItemList)
            {
                item.Marked = b;
            }
        }
        private void CopyButton_Click()
        {
            GlobalData.Inst.CopyAudioPathList.Clear();
            List<string> audioNameList = new();
            foreach (var item in LocalDetailItemList)
            {
                if (item.Marked)
                {
                    GlobalData.Inst.CopyAudioPathList.Add(
                        Path.Combine(tableItem.LocalItemPath, item.Header));
                    audioNameList.Add(item.Header);
                }
            }
            StringBuilder sb = new();
            sb.Append(LanguageManager.Inst.GetString("已复制"));
            sb.Append('\n');
            foreach (var name in audioNameList)
            {
                sb.Append($" {name}\n");
            }
            MainWindow.Inst.AddNotification(
                () => $"{LanguageManager.Inst.GetString("通知")}",
                () => $"{sb.ToString()}",
                Pages.LabelStatus.Success, 3.5f);
        }

        private void FolderButton_Click()
        {
            var path = tableItem.LocalItemPath;
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }

        private TablePageItem tableItem;
        private List<AudioData> AudioDataList = new();
        public async Task SetTableItem(TablePageItem tableItem)
        {
            Close();
            this.tableItem = tableItem;
            EditWorkshopButton.Visibility = SteamClient.IsValid && tableItem.Item.HasValue
                && tableItem.Item.Value.Owner.Id == SteamClient.SteamId ? Visibility.Visible : Visibility.Collapsed;
            int generation = _loadGeneration;
            _loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _loadCancellation.Token;
            FeedbackButton.SelectedIndex = -1;
            userVote = null;
            isFavorite = false;
            UpdateFeedbackState();
            AuthorPanel.Visibility = Visibility.Hidden;
            AuthorAvatar.Source = null;
            AuthorName.Text = string.Empty;
            ImageLoadingPanel.Visibility = Visibility.Visible;
            MetadataLoadingPanel.Visibility = Visibility.Visible;
            ImageLoadingBar.SetProgressInstant(0.08);
            MetadataLoadingBar.SetProgressInstant(0.08);

            try
            {
                Task authorTask = Task.CompletedTask;
                Task voteTask = Task.CompletedTask;
                if (tableItem.Item.HasValue)
                {
                    SteamScoreText.Text = $"★ {tableItem.Item.Value.Score * 5:0.0}";
                    authorTask = LoadAuthor(tableItem.Item.Value.Owner.Id, generation, cancellationToken);
                    voteTask = LoadVoteAsync(tableItem.Item.Value, generation, cancellationToken);
                }
                else
                {
                    SteamScoreText.Text = "★ --";
                }

                var path = tableItem.LocalItemPath;
                if (JsonTool.LoadJson<ResourceDataConfig>(Path.Combine(path, "ResourceConfig.json"), out var resourceDataConfig))
                {
                    ItemTitle.Text = resourceDataConfig.Title;
                    ResourceDescriptionText.Text = resourceDataConfig.Description ?? string.Empty;
                    ImageLoadingBar.SetProgress(0.45, 120);
                    BgImage.Source = WorkshopPage.LoadImage(Path.Combine(path, $"Thumbnail{resourceDataConfig.ThumbnailFormat}"));
                    await CompleteLocalImageProgressAsync(generation, cancellationToken);

                    var resourceItems = resourceDataConfig.Data?.ItemList ?? new List<ResourceDataConfig.ResourceItem>();
                    for (int i = 0; i < resourceItems.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!IsCurrentLoad(generation)) return;

                        var item = resourceItems[i];
                        if (File.Exists(Path.Combine(path, item.Name)))
                        {
                            var audioData = new AudioData();
                            var audioPath = Path.Combine(path, item.Name);
                            audioData.AudioTrackArray[0] = new AudioFileReader(audioPath);
                            audioData.AudioTrackArray[1] = new AudioFileReader(audioPath);
                            audioData.Index = i;
                            AudioDataList.Add(audioData);
                            LocalDetailItemList.Add(new LocalDetailPageItemViewModel(audioData, item));
                        }

                        MetadataLoadingBar.SetProgressInstant(0.12 + 0.62 * (i + 1) / Math.Max(1, resourceItems.Count));
                        await Task.Delay(2, cancellationToken);
                    }

                    if (IsCurrentLoad(generation))
                    {
                        long metadataTotalSize = resourceDataConfig.Data?.Size ?? 0;
                        long totalSize = metadataTotalSize > 0
                            ? metadataTotalSize
                            : resourceItems.Aggregate(
                                0L,
                                (total, resourceItem) => checked(total + resourceItem.Size));
                        ResourceSizeText.Text = AudioData.SizeToString(totalSize);
                        ResourceSizeText.Visibility = Visibility.Visible;
                    }

                    MetadataLoadingBar.SetProgress(0.82, 180);
                    await CheckSubscribe(tableItem);
                }

                await Task.WhenAll(authorTask, voteTask);
                if (!IsCurrentLoad(generation)) return;

                MetadataLoadingBar.SetProgress(1, 120);
                await Task.Delay(100, cancellationToken);
                MetadataLoadingPanel.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AppLog.Debug("本地创意工坊详情加载已取消。", "Workshop.LocalDetail");
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "加载本地创意工坊详情失败。", "Workshop.LocalDetail");
                if (IsCurrentLoad(generation))
                {
                    ImageLoadingPanel.Visibility = Visibility.Collapsed;
                    MetadataLoadingPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void EditWorkshop_Click(object sender, RoutedEventArgs e)
        {
            if (tableItem?.Item is Steamworks.Ugc.Item item)
                await WorkshopPage.Inst.OpenWorkshopEditorAsync(item);
        }

        public void Close()
        {
            _loadGeneration++;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            for (int i = 0; i < AudioDataList.Count; i++)
            {
                var data = AudioDataList[i];
                data.Dispose();
            }
            tableItem = null;
            EditWorkshopButton.Visibility = Visibility.Collapsed;
            LocalDetailItemList.Clear();
            AudioDataList.Clear();
            BgImage.Source = null;
            AuthorPanel.Visibility = Visibility.Hidden;
            AuthorAvatar.Source = null;
            AuthorName.Text = string.Empty;
            ResourceDescriptionText.Text = string.Empty;
            ResourceSizeText.Text = string.Empty;
            ResourceSizeText.Visibility = Visibility.Collapsed;
            SteamScoreText.Text = "★ --";
            FeedbackButton.SelectedIndex = -1;
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

        private async Task LoadVoteAsync(Steamworks.Ugc.Item item, int generation, CancellationToken cancellationToken)
        {
            var vote = await item.GetUserVote();
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentLoad(generation)) return;

            userVote = vote?.VotedUp == true ? true : vote?.VotedDown == true ? false : null;
            UpdateFeedbackState();
        }

        private bool IsCurrentLoad(int generation) =>
            generation == _loadGeneration && tableItem != null;

        private async Task CompleteLocalImageProgressAsync(int generation, CancellationToken cancellationToken)
        {
            ImageLoadingBar.SetProgress(0.78, 140);
            await Task.Delay(140, cancellationToken);
            ImageLoadingBar.SetProgress(1, 120);
            await Task.Delay(120, cancellationToken);
            if (IsCurrentLoad(generation))
                ImageLoadingPanel.Visibility = Visibility.Collapsed;
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

        public ObservableCollection<LocalDetailPageItemViewModel> LocalDetailItemList { get; set; } = new();
    }
}
