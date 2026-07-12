using Microsoft.Win32;
using PlayVoice.Resources.Language;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlayVoice.Pages.Preset
{
    public partial class AudioTrackGrid : Page
    {

        public AudioTrackGrid()
        {
            InitializeComponent();
            this.DataContext = this;
            TopButtonGroupListBox.SelectionChanged += TopButtonGroupOnSelection;
            this.Unloaded += AudioTrackGrid_Unloaded;
        }


        public ObservableCollection<AudioTrackItemViewModel> ShortcutList { get; set; } = new();

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

        private async void TopButtonGroupOnSelection(object sender, SelectionChangedEventArgs e)
        {
            if (TopButtonGroupListBox.SelectedIndex == -1) return;
            var intdex = TopButtonGroupListBox.SelectedIndex;

            TopButtonGroupListBox.SelectedIndex = -1;
           
            switch (intdex)
            {
                case 0: SelectAllButton_Click(); break;
                case 1: CopyButton_Click(); break;
                case 2: PasteButton_Click(); break;
                case 3: await Task.Delay(75); ImportButton_Click(); break;
                case 4: await Task.Delay(75); UploadButton_Click(); break;
                case 5: DeleteButton_Click(); break;
                case 6: FolderButton_Click(); break;
            }
        }
        private void SelectAllButton_Click()
        {
            var b = !SelectAll;
            SelectAll = b;
            foreach (var item in ShortcutList)
            {
                item.Marked = b;
            }
        }

        private void CopyButton_Click()
        {
            GlobalData.Inst.CopyAudioPathList.Clear();
            List<string> audioNameList = new();
            foreach (var item in ShortcutList)
            {
                if (item.Marked)
                {
                    GlobalData.Inst.CopyAudioPathList.Add(
                        Path.Combine(PresetDataTool.basePath, GlobalData.Inst.PresetData.Config.Name, item.Data.Config.Name));
                    audioNameList.Add(item.Data.Config.Name);
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

        private void PasteButton_Click()
        {
            foreach (var path in GlobalData.Inst.CopyAudioPathList)
            {
                if (GlobalData.Inst.PresetData.AddAudio(path, out var audioData))
                {

                }
            }
            GlobalData.Inst.PresetData.Save();
            InitLoadPreset(GlobalData.Inst.PresetData.Config.Name);
        }

        private void ImportButton_Click()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = LanguageManager.Inst.GetString("添加音频");
            openFileDialog.Filter = $"{LanguageManager.Inst.GetString("音频文件")} (*.mp3;*.wav;*.m4a;*.flac)|*.mp3;*.wav;*.m4a;*.flac";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = true;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string[] selectedFiles = openFileDialog.FileNames;
                foreach (string file in selectedFiles)
                {
                    if (GlobalData.Inst.PresetData.AddAudio(file, out var audioData))
                    {

                    }
                }
                GlobalData.Inst.PresetData.Save();
                InitLoadPreset(GlobalData.Inst.PresetData.Config.Name);
            }
        }

        private void UploadButton_Click()
        {
            PresetPage.Inst.TopButtonListBox.SelectedIndex = PresetPage.Inst.Count - 1;
        }

        private void DeleteButton_Click()
        {
            foreach (var item in ShortcutList)
            {
                if (item.Marked)
                {
                    var index = item.Data.Index;
                    GlobalData.Inst.PresetData.RemoveAudio(index);
                }
            }
            GlobalData.Inst.PresetData.Save();
            InitLoadPreset(GlobalData.Inst.PresetData.Config.Name);
        }


        private void FolderButton_Click()
        {
            var path = Path.Combine(PresetDataTool.basePath, GlobalData.Inst.PresetData.Config.Name);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }


        // 悬停高频触发：精准控制提示条位置
        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("AudioTrackItemVisual"))
            {
                e.Effects = DragDropEffects.Move;
                var dragItem = e.Data.GetData("AudioTrackItemVisual") as AudioTrackItem;
                if (dragItem == null) return;

                Point mousePos = e.GetPosition(DragOverlayCanvas);

                // --- 1. 处理跟随鼠标的虚化影子 ---
                if (DragGhost.Visibility == Visibility.Collapsed)
                {
                    if (dragItem.Tag is RenderTargetBitmap bmp)
                    {
                        DragGhost.Fill = new ImageBrush(bmp);
                    }
                    else
                    {
                        DragGhost.Fill = new VisualBrush(dragItem);
                    }
                    DragGhost.Width = dragItem.ActualWidth;
                    DragGhost.Height = dragItem.ActualHeight;
                    DragGhost.Visibility = Visibility.Visible;
                    InsertionLine.Width = dragItem.ActualWidth;
                }
                Canvas.SetLeft(DragGhost, mousePos.X - 25);
                Canvas.SetTop(DragGhost, mousePos.Y - dragItem.ActualHeight / 2);

                // 获取当前正在拖拽的项的原始索引
                int oldIndex = ShortcutList.IndexOf(dragItem.DataContext as AudioTrackItemViewModel);

                // --- 2. 核心修复：最顶部磁吸安全边界判定 ---
                if (ShortcutList.Count > 0)
                {
                    var firstItem = AudioListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    if (firstItem != null)
                    {
                        Point mouseInFirst = e.GetPosition(firstItem);
                        // 如果鼠标超出了第一个项的顶边（Y < 0），或者处于第一个项的上半段
                        if (mouseInFirst.Y < firstItem.ActualHeight / 2)
                        {
                            Point itemPos = firstItem.TransformToVisual(DragOverlayCanvas).Transform(new Point(0, 0));
                            // 高亮条强行吸附在绝对最顶部（第一项的上方）
                            Canvas.SetLeft(InsertionLine, itemPos.X);
                            Canvas.SetTop(InsertionLine, itemPos.Y - 1.5);
                            InsertionLine.Visibility = Visibility.Visible;
                            e.Handled = true;
                            return; // 提前拦截，不走后面的下方卡位逻辑
                        }
                    }
                }

                // --- 3. 正常的项落位判定 ---
                DependencyObject target = e.OriginalSource as DependencyObject;
                while (target != null && !(target is ListBoxItem))
                {
                    target = VisualTreeHelper.GetParent(target);
                }

                if (target is ListBoxItem listBoxItem)
                {
                    var targetData = listBoxItem.DataContext as AudioTrackItemViewModel;
                    int targetIndex = ShortcutList.IndexOf(targetData);
                    Point itemPos = listBoxItem.TransformToVisual(DragOverlayCanvas).Transform(new Point(0, 0));

                    // 核心修复：如果鼠标在自己隐藏的空白空间内部
                    if (targetIndex == oldIndex)
                    {
                        if (oldIndex == 0)
                        {
                            // 如果是第一个项在自己的空白空间内，高亮条依然无脑吸附在最顶部，极大方便放回原位
                            Canvas.SetLeft(InsertionLine, itemPos.X);
                            Canvas.SetTop(InsertionLine, itemPos.Y - 1.5);
                        }
                        else
                        {
                            // 其他项在自己空间内，保持在下方即可
                            Canvas.SetLeft(InsertionLine, itemPos.X);
                            Canvas.SetTop(InsertionLine, itemPos.Y + listBoxItem.ActualHeight - 1.5);
                        }
                    }
                    else
                    {
                        // 正常拖到别的项上面，永远显示在当前项的下方
                        Canvas.SetLeft(InsertionLine, itemPos.X);
                        Canvas.SetTop(InsertionLine, itemPos.Y + listBoxItem.ActualHeight - 1.5);
                    }
                    InsertionLine.Visibility = Visibility.Visible;
                }
                else
                {
                    // 如果鼠标彻底滑到了列表最下方的空白处（由于上方已经拦截了最顶部越界，这里一定是底部越界）
                    if (ShortcutList.Count > 0)
                    {
                        var lastItem = AudioListBox.ItemContainerGenerator.ContainerFromIndex(ShortcutList.Count - 1) as ListBoxItem;
                        if (lastItem != null)
                        {
                            Point itemPos = lastItem.TransformToVisual(DragOverlayCanvas).Transform(new Point(0, 0));
                            Canvas.SetLeft(InsertionLine, itemPos.X);
                            Canvas.SetTop(InsertionLine, itemPos.Y + lastItem.ActualHeight - 1.5);
                            InsertionLine.Visibility = Visibility.Visible;
                        }
                    }
                }
                e.Handled = true;
            }
        }

        // 鼠标松开放置：精准落位
        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            HideDragVisuals();

            if (e.Data.GetDataPresent("AudioTrackItemVisual"))
            {
                var dragItem = e.Data.GetData("AudioTrackItemVisual") as AudioTrackItem;
                var droppedData = dragItem?.DataContext as AudioTrackItemViewModel;
                if (droppedData == null) return;

                DependencyObject target = e.OriginalSource as DependencyObject;
                while (target != null && !(target is ListBoxItem))
                {
                    target = VisualTreeHelper.GetParent(target);
                }

                int oldIndex = ShortcutList.IndexOf(droppedData);
                int newIndex = -1;

                // 核心修复：松手时先拦截是否触发了最顶部判定
                if (ShortcutList.Count > 0)
                {
                    var firstItem = AudioListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                    if (firstItem != null)
                    {
                        Point mouseInFirst = e.GetPosition(firstItem);
                        if (mouseInFirst.Y < firstItem.ActualHeight / 2)
                        {
                            newIndex = 0; // 只要满足最顶判定，无条件归位到 0
                        }
                    }
                }

                // 如果没有触发最顶部判定，再计算普通的项位置
                if (newIndex == -1)
                {
                    if (target is ListBoxItem listBoxItem)
                    {
                        var targetData = listBoxItem.DataContext as AudioTrackItemViewModel;
                        if (targetData != null)
                        {
                            int targetIndex = ShortcutList.IndexOf(targetData);

                            if (targetIndex == oldIndex)
                            {
                                newIndex = oldIndex; // 如果放到了自己隐藏的占位格子上，原位不动
                            }
                            else
                            {
                                newIndex = (oldIndex < targetIndex) ? targetIndex : targetIndex + 1;
                            }
                        }
                    }
                    else
                    {
                        newIndex = ShortcutList.Count - 1; // 真正的底部空白区域
                    }
                }

                // 安全边界限制
                if (newIndex < 0) newIndex = 0;
                if (newIndex >= ShortcutList.Count) newIndex = ShortcutList.Count - 1;

                // 执行集合移动
                if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
                {
                    ShortcutList.Move(oldIndex, newIndex);
                    GlobalData.Inst.PresetData.SwapOrder(oldIndex, newIndex);
                    GlobalData.Inst.PresetData.Save();
                }
            }
        }

        private void HideDragVisuals()
        {
            DragGhost.Visibility = Visibility.Collapsed;
            InsertionLine.Visibility = Visibility.Collapsed;
        }

        private void ListBox_DragLeave(object sender, DragEventArgs e)
        {
            HideDragVisuals();
        }


        public async Task InitLoadPreset(string name)
        {
            if (GlobalData.Inst.PresetData != null)
            {
                ShortcutList.Clear();
                GlobalData.Inst.PresetData = null;
            }
            TopButtonGroupListBox.SelectedIndex = -1;
            SelectAll = false;
            GlobalData.Inst.PresetData = await PresetDataTool.LoadPresetData(name);
            var presetData = GlobalData.Inst.PresetData;
            foreach (var item in presetData.AudioList)
            {
                ShortcutList.Add(new AudioTrackItemViewModel(item));
                item.AnimationPlayAction = () =>
                {
                    var listBoxItem = AudioListBox.ItemContainerGenerator.ContainerFromIndex(item.Index) as ListBoxItem;
                    if (listBoxItem != null)
                    {
                        listBoxItem.ApplyTemplate();
                        var trackItem = FindVisualChild<AudioTrackItem>(listBoxItem);
                        if (trackItem != null)
                        {
                            trackItem.PlayClickAnimation();
                        }
                    }
                };
                item.AnimationStopAction = () =>
                {
                    var listBoxItem = AudioListBox.ItemContainerGenerator.ContainerFromIndex(item.Index) as ListBoxItem;
                    if (listBoxItem != null)
                    {
                        listBoxItem.ApplyTemplate();
                        var trackItem = FindVisualChild<AudioTrackItem>(listBoxItem);
                        if (trackItem != null)
                        {
                            trackItem.ResetAnimation();
                        }
                    }
                };
                await Task.Delay(1);
            }
        }



        public void Leave()
        {
            var presetData = GlobalData.Inst.PresetData;
            if (presetData == null) return;
            foreach (var item in presetData.AudioList)
            {
                item.AnimationPlayAction = null;
                item.AnimationStopAction = null;
            }
            GlobalData.Inst.PresetData.Save();
        }

        private void AudioTrackGrid_Unloaded(object sender, RoutedEventArgs e) => Leave();

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}