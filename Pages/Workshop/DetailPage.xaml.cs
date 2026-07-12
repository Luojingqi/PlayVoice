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
        }



        private ObservableCollection<DetailPageItem> DetailItemList = new();

        private TablePageItem tableItem;
        public async Task SetTableItem(TablePageItem tableItem)
        {
            Close();
            this.tableItem = tableItem;
            var item = tableItem.Item.Value;
            ItemTitle.Text = item.Title;
            BgImage.Source = await WorkshopPage.DownloadImageAsBitmapAsync(item);
            var metaData = JsonTool.ToObject<ResourceDataConfig.Metadata>(item.Metadata);
            foreach (var data in metaData.ItemList)
            {
                var detailItem = new DetailPageItem(data);
                DetailItemList.Add(detailItem);
            }
            CheckSubscribe();
        }

        public void Close()
        {
            tableItem = null;
            DetailItemList.Clear();
            BgImage.Source = null;
            SubscribeButton.Visibility = Visibility.Collapsed;
            UnSubscribeButton.Visibility = Visibility.Collapsed;
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
