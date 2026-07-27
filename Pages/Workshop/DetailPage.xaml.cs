using PlayVoice.Resources.Language;
using Steamworks;
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
            var item = tableItem.Item.Value;
            ItemTitle.Text = item.Title;
            SteamScoreText.Text = $"★ {item.Score * 5:0.0}";
            BgImage.Source = await WorkshopPage.DownloadImageAsBitmapAsync(item);
            await LoadAuthor(item.Owner.Id);
            var metaData = JsonTool.ToObject<ResourceDataConfig.Metadata>(item.Metadata);
            foreach (var data in metaData.ItemList)
            {
                var detailItem = new DetailPageItem(data);
                DetailItemList.Add(detailItem);
            }
            CheckSubscribe();
            var vote = await item.GetUserVote();
            userVote = vote?.VotedUp == true ? true : vote?.VotedDown == true ? false : null;
            UpdateFeedbackState();
        }

        public void Close()
        {
            tableItem = null;
            DetailItemList.Clear();
            BgImage.Source = null;
            SubscribeButton.Visibility = Visibility.Collapsed;
            UnSubscribeButton.Visibility = Visibility.Collapsed;
            FeedbackButton.SelectedIndex = -1;
            AuthorPanel.Visibility = Visibility.Hidden;
            AuthorAvatar.Source = null;
            AuthorName.Text = string.Empty;
            SteamScoreText.Text = "★ --";
            userVote = null;
            isFavorite = false;
            UpdateFeedbackState();
        }

        private async Task LoadAuthor(SteamId owner)
        {
            var author = await WorkshopAuthorInfo.LoadAsync(owner);
            if (tableItem == null || tableItem.Item.Value.Owner.Id != owner) return;

            AuthorName.Text = author.Name;
            AuthorAvatar.Source = author.Avatar;
            AuthorPanel.Visibility = Visibility.Visible;
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

        private async Task<bool> CheckSubscribe()
        {
            var item = await SteamUGC.QueryFileAsync(tableItem.Item.Value.Id);
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
