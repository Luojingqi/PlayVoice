using Microsoft.Win32;
using PlayVoice.Audio;
using System.Windows.Input;
using PlayVoice.Pages.Loading;
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
            audioFileProgressOverlay.StopRequested += AudioFileProgressOverlay_StopRequested;
            this.Unloaded += AudioTrackGrid_Unloaded;
            Loaded += AudioTrackGrid_Loaded;
            AudioListBox.PreviewMouseRightButtonDown += AudioListBox_RightClick;
        }


        public ObservableCollection<AudioTrackItemViewModel> ShortcutList { get; set; } = new();
        private bool isAddingAudio;
        private readonly AudioFileProgressOverlay audioFileProgressOverlay = new();
        private CancellationTokenSource addAudioCancellation;
        private CancellationTokenSource audioPresetLoadCancellation;
        private int audioTrackRefreshVersion;

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
                case 2: await PasteButton_Click(); break;
                case 3: await StartImportAsync(); break;
                case 4: await Task.Delay(75); UploadButton_Click(); break;
                case 5: await DeleteButton_Click(); break;
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
                        Path.Combine(AudioPresetDataTool.BasePath,
                            GlobalData.Inst.ActiveAudioPreset.Config.Name, item.Data.Config.Name));
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

        private async Task PasteButton_Click()
        {
            string[] copiedFiles = GlobalData.Inst.CopyAudioPathList.ToArray();
            await AddAudioFilesAsync(
                copiedFiles,
                LanguageManager.Inst.GetString("粘贴"));
        }

        private async Task StartImportAsync()
        {
            if (isAddingAudio) return;

            await Task.Delay(75);
            await ImportButton_Click();
        }

        private async Task ImportButton_Click()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = LanguageManager.Inst.GetString("添加音频");
            openFileDialog.Filter = $"{LanguageManager.Inst.GetString("音频文件")} (*.mp3;*.wav;*.m4a;*.flac;*.aac)|*.mp3;*.wav;*.m4a;*.flac;*.aac";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = true;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string[] selectedFiles = openFileDialog.FileNames;
                await AddAudioFilesAsync(
                    selectedFiles,
                    LanguageManager.Inst.GetString("导入"));
            }
        }

        private async Task AddAudioFilesAsync(
            IReadOnlyList<string> filePaths,
            string operationTitle)
        {
            if (isAddingAudio || filePaths.Count == 0)
                return;

            var targetPreset = GlobalData.Inst.ActiveAudioPreset;
            if (targetPreset == null)
                return;

            using var cancellationSource = new CancellationTokenSource();
            addAudioCancellation = cancellationSource;
            isAddingAudio = true;
            audioFileProgressOverlay.Show(operationTitle, filePaths.Count);
            MainWindow.Inst.ShowBodyInteractionOverlay(audioFileProgressOverlay);
            try
            {
                for (int i = 0; i < filePaths.Count; i++)
                {
                    cancellationSource.Token.ThrowIfCancellationRequested();
                    string file = filePaths[i];
                    audioFileProgressOverlay.SetProgress(
                        (double)i / filePaths.Count,
                        file,
                        i + 1,
                        filePaths.Count);

                    await targetPreset.AddAudio(file, cancellationSource.Token);
                    audioFileProgressOverlay.SetProgress(
                        (double)(i + 1) / filePaths.Count,
                        file,
                        i + 1,
                        filePaths.Count);
                    await Task.Delay(25, cancellationSource.Token);
                }
            }
            catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
            {
            }
            finally
            {
                try
                {
                    targetPreset.Save();
                    if (ReferenceEquals(GlobalData.Inst.ActiveAudioPreset, targetPreset))
                    {
                        GlobalData.Inst.RebuildActiveHotkeys();
                        await RefreshCurrentAudioPreset();
                    }
                }
                finally
                {
                    MainWindow.Inst.HideBodyInteractionOverlay(audioFileProgressOverlay);
                    audioFileProgressOverlay.Hide();
                    addAudioCancellation = null;
                    isAddingAudio = false;
                }
            }
        }

        private void AudioFileProgressOverlay_StopRequested(object sender, EventArgs e)
        {
            addAudioCancellation?.Cancel();
        }

        private void UploadButton_Click()
        {
            PresetPage.Inst.SelectCreateManagementPage();
        }

        private async Task DeleteButton_Click()
        {
            var selectedIndexes = ShortcutList
                .Where(item => item.Marked)
                .Select(item => item.Data.Index)
                .OrderByDescending(index => index)
                .ToArray();
            foreach (int index in selectedIndexes)
                GlobalData.Inst.ActiveAudioPreset.RemoveAudio(index);

            GlobalData.Inst.ActiveAudioPreset.Save();
            await RefreshCurrentAudioPreset();
        }


        private void FolderButton_Click()
        {
            var path = Path.Combine(AudioPresetDataTool.BasePath,
                GlobalData.Inst.ActiveAudioPreset.Config.Name);
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
                bool supported = !isAddingAudio && files.Any(IsSupportedFile);
                e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None;
                FileDropHint.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
                e.Handled = true;
                return;
            }
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
        private async void ListBox_Drop(object sender, DragEventArgs e)
        {
            HideDragVisuals();
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                await ImportDroppedFiles(e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>());
                return;
            }

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
                    GlobalData.Inst.ActiveAudioPreset.SwapOrder(oldIndex, newIndex);
                    GlobalData.Inst.ActiveAudioPreset.Save();
                }
            }
        }

        private void HideDragVisuals()
        {
            FileDropHint.Visibility = Visibility.Collapsed;
            DragGhost.Visibility = Visibility.Collapsed;
            InsertionLine.Visibility = Visibility.Collapsed;
        }

        private void ListBox_DragLeave(object sender, DragEventArgs e)
        {
            HideDragVisuals();
        }


        public async Task InitLoadAudioPreset(string name)
        {
            if (!PresetPage.Inst.IsAudioPresetPageSelected(name))
                return;

            if (GlobalData.Inst.ActiveAudioPreset?.Config.Name == name)
            {
                await RefreshCurrentAudioPreset();
                return;
            }
            CancelPendingAudioPresetLoad(clearItems: true);
            using var cancellationSource = new CancellationTokenSource();
            audioPresetLoadCancellation = cancellationSource;
            AudioPresetData presetData = null;
            try
            {
                presetData = await AudioPresetDataTool.LoadAudioPresetData(
                    name,
                    cancellationSource.Token);
                cancellationSource.Token.ThrowIfCancellationRequested();

                if (!PresetPage.Inst.IsAudioPresetPageSelected(name))
                    return;

                GlobalData.Inst.ActiveAudioPreset = presetData;
                presetData = null; // Ownership is now held by GlobalData.
                await RefreshCurrentAudioPreset(cancellationSource.Token);
            }
            catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
            {
            }
            finally
            {
                presetData?.Dispose();
                if (ReferenceEquals(audioPresetLoadCancellation, cancellationSource))
                    audioPresetLoadCancellation = null;
            }
        }

        public async Task RefreshCurrentAudioPreset(
            CancellationToken cancellationToken = default)
        {
            GlobalData.Inst.Playback.NotifyChanged();
            int refreshVersion = ++audioTrackRefreshVersion;
            ShortcutList.Clear();
            TopButtonGroupListBox.SelectedIndex = -1;
            SelectAll = false;
            var presetData = GlobalData.Inst.ActiveAudioPreset;
            if (presetData == null) return;

            foreach (var item in presetData.AudioList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (refreshVersion != audioTrackRefreshVersion
                    || !ReferenceEquals(presetData, GlobalData.Inst.ActiveAudioPreset)
                    || !PresetPage.Inst.IsAudioPresetPageSelected(presetData.Config.Name))
                    return;

                ShortcutList.Add(new AudioTrackItemViewModel(item));
                await Task.Delay(1, cancellationToken);
            }
        }

        public void CancelPendingAudioPresetLoad(bool clearItems)
        {
            var cancellationSource = audioPresetLoadCancellation;
            audioPresetLoadCancellation = null;
            cancellationSource?.Cancel();
            audioTrackRefreshVersion++;

            if (!clearItems)
                return;

            ShortcutList.Clear();
            TopButtonGroupListBox.SelectedIndex = -1;
            SelectAll = false;
        }

        public void RefreshFunctionPresetValues()
        {
            foreach (var item in ShortcutList)
                item.RefreshFunctionPresetValues();
        }



        private NativeFileDrop nativeFileDrop;
        private bool syncingPlaybackMode;

        private void AudioTrackGrid_Loaded(object sender, RoutedEventArgs e)
        {
            GlobalData.Inst.Playback.Changed += RefreshPlaybackMode;
            RefreshPlaybackMode();
            nativeFileDrop = new NativeFileDrop(AudioListBox, ImportDroppedFiles);
        }

        private void RefreshPlaybackMode()
        {
            syncingPlaybackMode = true;
            PlaybackModeListBox.SelectedIndex = (int)GlobalData.Inst.Playback.Mode;
            syncingPlaybackMode = false;
        }

        private void PlaybackMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!syncingPlaybackMode && PlaybackModeListBox.SelectedIndex >= 0)
                GlobalData.Inst.Playback.SetMode((PlaybackMode)PlaybackModeListBox.SelectedIndex);
        }

        private static bool IsSupportedFile(string path) => File.Exists(path)
            && new[] { ".mp3", ".wav", ".m4a", ".flac", ".aac" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private async Task ImportDroppedFiles(string[] files)
        {
            if (isAddingAudio || !IsLoaded || !IsEnabled) return;
            var supported = files.Where(IsSupportedFile).ToArray();
            if (supported.Length != files.Length)
                MainWindow.Inst.AddNotification(
                    () => LanguageManager.Inst.GetString("通知"),
                    () => LanguageManager.Inst.GetString("已忽略不支持的文件"), Pages.LabelStatus.Warning, 4);
            await AddAudioFilesAsync(supported, LanguageManager.Inst.GetString("导入"));
        }

        private void AudioListBox_RightClick(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not AudioTrackItem)
                element = VisualTreeHelper.GetParent(element);
            if (element is not AudioTrackItem row) return;
            var target = row.ViewModel;
            if (!target.Marked)
            {
                foreach (var item in ShortcutList) item.Marked = item == target;
                SelectAll = false;
            }
            var menu = new ContextMenu();
            menu.SetResourceReference(Control.BackgroundProperty, "BgMedium");
            menu.SetResourceReference(Control.ForegroundProperty, "TextPrimary");
            void Add(string key, Action action, bool enabled = true)
            {
                var item = new MenuItem { Header = LanguageManager.Inst.GetString(key), IsEnabled = enabled };
                item.Click += (_, _) => action();
                menu.Items.Add(item);
            }
            Add("选择", () => target.Marked = !target.Marked);
            Add("复制", CopyButton_Click);
            var delete = new MenuItem { Header = LanguageManager.Inst.GetString("删除") };
            delete.Click += async (_, _) => await DeleteButton_Click();
            menu.Items.Add(delete);
            Add("文件资源浏览器", FolderButton_Click);
            menu.Items.Add(new Separator());
            Add("向上移动", () => MoveSelected(-1), CanMoveSelected(-1));
            Add("向下移动", () => MoveSelected(1), CanMoveSelected(1));
            menu.Items.Add(new Separator());
            Add("重命名", () => RenameTrack(target), ShortcutList.Count(x => x.Marked) == 1);
            menu.PlacementTarget = row;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private bool CanMoveSelected(int direction) => ShortcutList.Select((item, i) => (item, i))
            .Any(x => x.item.Marked && x.i + direction >= 0 && x.i + direction < ShortcutList.Count
                && !ShortcutList[x.i + direction].Marked);

        private void MoveSelected(int direction)
        {
            var indexes = Enumerable.Range(0, ShortcutList.Count);
            if (direction > 0) indexes = indexes.Reverse();
            foreach (int i in indexes)
            {
                int next = i + direction;
                if (!ShortcutList[i].Marked || next < 0 || next >= ShortcutList.Count || ShortcutList[next].Marked) continue;
                ShortcutList.Move(i, next);
                GlobalData.Inst.ActiveAudioPreset.SwapOrder(i, next);
            }
            GlobalData.Inst.ActiveAudioPreset.Save();
        }

        private void RenameTrack(AudioTrackItemViewModel target)
        {
            var window = new RenameAudioWindow(target.Data) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) target.Header = target.Data.Config.Name;
        }

        public void Leave()
        {
            GlobalData.Inst.ActiveAudioPreset?.Save();
        }

        private void AudioTrackGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            nativeFileDrop?.Dispose();
            nativeFileDrop = null;
            GlobalData.Inst.Playback.Changed -= RefreshPlaybackMode;
            addAudioCancellation?.Cancel();
            CancelPendingAudioPresetLoad(clearItems: false);
            Leave();
        }
    }
}
