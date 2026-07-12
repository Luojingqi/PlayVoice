using NAudio.Wave;
using PlayVoice.Audio;
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
            this.tableItem = tableItem;
            var path = tableItem.LocalItemPath;
            if (JsonTool.LoadJson<ResourceDataConfig>(Path.Combine(path, "ResourceConfig.json"), out var resourceDataConfig))
            {
                ItemTitle.Text = resourceDataConfig.Title;
                BgImage.Source = WorkshopPage.LoadImage(Path.Combine(path, $"Thumbnail{resourceDataConfig.ThumbnailFormat}"));
                LocalDetailItemList.Clear();
                AudioDataList.Clear();
                for (int i = 0; i < resourceDataConfig.Data.ItemList.Count; i++)
                {
                    var item = resourceDataConfig.Data.ItemList[i];
                    if (File.Exists(Path.Combine(path, $"{item.FileName}{item.FileFormat}")))
                    {
                        var audioData = new AudioData();
                        var audioPath = Path.Combine(path, $"{item.FileName}{item.FileFormat}");
                        audioData.AudioTrackArray[0] = new AudioFileReader(audioPath);
                        audioData.AudioTrackArray[1] = new AudioFileReader(audioPath);
                        audioData.Index = i;
                        AudioDataList.Add(audioData);
                        LocalDetailItemList.Add(new LocalDetailPageItemViewModel(audioData, item));
                    }
                    await Task.Delay(2);
                }
                CheckSubscribe();
            }
        }

        public void Close()
        {
            for (int i = 0; i < AudioDataList.Count; i++)
            {
                var data = AudioDataList[i];
                data.Dispose();
            }
            tableItem = null;
            LocalDetailItemList.Clear();
            AudioDataList.Clear();
            BgImage.Source = null;
        }

        private async Task<bool> CheckSubscribe()
        {
            if (tableItem.Item.HasValue == false) return false;
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

        public ObservableCollection<LocalDetailPageItemViewModel> LocalDetailItemList { get; set; } = new();
    }
}
