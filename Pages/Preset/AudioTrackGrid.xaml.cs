using Microsoft.Win32;
using PlayVoice.Audio;
using System.Windows.Input;
using PlayVoice.Pages.Loading;
using PlayVoice.Resources.Language;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
            InitializeLibrary();
            SizeChanged += (_, e) => LibraryInspector.IsExpanded = e.NewSize.Width >= 1000;
            this.DataContext = this;
            TopButtonGroupListBox.SelectionChanged += TopButtonGroupOnSelection;
            audioFileProgressOverlay.StopRequested += AudioFileProgressOverlay_StopRequested;
            this.Unloaded += AudioTrackGrid_Unloaded;
            Loaded += AudioTrackGrid_Loaded;
            AudioListBox.PreviewMouseRightButtonDown += AudioListBox_RightClick;
        }


        private sealed class TrackCollection : ObservableCollection<AudioTrackItemViewModel>
        {
            public void ReplaceAll(IReadOnlyList<AudioTrackItemViewModel> rows)
            {
                Items.Clear();
                foreach (var row in rows) Items.Add(row);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private TrackCollection ShortcutList { get; } = new();
        private AudioPresetData displayedPreset;
        private long displayedTracksVersion;
        private long recentVersion;
        private bool isAddingAudio;
        private readonly AudioFileProgressOverlay audioFileProgressOverlay = new();
        private CancellationTokenSource addAudioCancellation;

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
                case 5: DeleteButton_Click(); break;
                case 6: FolderButton_Click(); break;
            }
        }
        private void SelectAllButton_Click()
        {
            var b = !SelectAll;
            SelectAll = b;
            foreach (var item in AudioView.Cast<AudioTrackItemViewModel>())
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
            catch (Exception exception)
            {
                ShowLibraryError(exception);
            }
            finally
            {
                try
                {
                    targetPreset.Save();
                    if (ReferenceEquals(GlobalData.Inst.ActiveAudioPreset, targetPreset))
                    {
                        GlobalData.Inst.RebuildActiveHotkeys();
                        RefreshCurrentAudioPreset();
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

        private void DeleteButton_Click()
        {
            var selectedIndexes = ShortcutList
                .Where(item => item.Marked)
                .Select(item => item.Data.Index)
                .OrderByDescending(index => index)
                .ToArray();
            foreach (int index in selectedIndexes)
                GlobalData.Inst.ActiveAudioPreset.RemoveAudio(index);

            GlobalData.Inst.ActiveAudioPreset.Save();
            RefreshCurrentAudioPreset();
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
            if (e.Data.GetData(typeof(AudioTrackItemViewModel)) is not AudioTrackItemViewModel) return;
            e.Handled = true;
            e.Effects = DragDropEffects.None;
            if (IsLibraryFiltered || !TryGetInsertionPoint(e, out _, out var line))
            {
                HideDragVisuals();
                return;
            }

            e.Effects = DragDropEffects.Move;
            if (e.Data.GetData("AudioTrackDragImage") is BitmapSource image)
            {
                if (DragGhost.Visibility != Visibility.Visible)
                {
                    DragGhost.Fill = new ImageBrush(image);
                    DragGhost.Width = image.Width;
                    DragGhost.Height = image.Height;
                    DragGhost.Visibility = Visibility.Visible;
                }
                var mouse = e.GetPosition(DragOverlayCanvas);
                Canvas.SetLeft(DragGhost, mouse.X - 25);
                Canvas.SetTop(DragGhost, mouse.Y - image.Height / 2);
            }
            Canvas.SetLeft(InsertionLine, line.X);
            Canvas.SetTop(InsertionLine, line.Y - InsertionLine.Height / 2);
            InsertionLine.Width = DragGhost.Visibility == Visibility.Visible ? DragGhost.Width : AudioListBox.ActualWidth;
            InsertionLine.Visibility = Visibility.Visible;
        }

        private bool TryGetInsertionPoint(DragEventArgs e, out int index, out Point line)
        {
            index = -1;
            line = default;
            if (ShortcutList.Count == 0 || e.OriginalSource is not DependencyObject source) return false;
            var row = ItemsControl.ContainerFromElement(AudioListBox, source) as ListBoxItem;
            if (row == null)
            {
                // Empty space beyond a realized edge is a valid target; a scrollbar is not.
                var first = AudioListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                row = first != null && e.GetPosition(first).Y <= 0 ? first
                    : AudioListBox.ItemContainerGenerator.ContainerFromIndex(ShortcutList.Count - 1) as ListBoxItem;
                if (row == null) return false;
                var point = e.GetPosition(row);
                if (point.X < 0 || point.X > row.ActualWidth || (row != first && point.Y < row.ActualHeight)) return false;
            }
            int rowIndex = AudioListBox.ItemContainerGenerator.IndexFromContainer(row);
            if (rowIndex < 0) return false;
            bool after = e.GetPosition(row).Y >= row.ActualHeight / 2;
            index = after ? rowIndex + 1 : rowIndex;
            line = row.TranslatePoint(new Point(0, after ? row.ActualHeight : 0), DragOverlayCanvas);
            return true;
        }

        private async void ListBox_Drop(object sender, DragEventArgs e)
        {
            HideDragVisuals();
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                await ImportDroppedFiles(e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>());
                return;
            }

            if (e.Data.GetData(typeof(AudioTrackItemViewModel)) is not AudioTrackItemViewModel dragged) return;
            e.Handled = true;
            if (IsLibraryFiltered || !TryGetInsertionPoint(e, out int insertion, out _)) return;
            int oldIndex = ShortcutList.IndexOf(dragged);
            if (oldIndex < 0) return;
            int newIndex = insertion > oldIndex ? insertion - 1 : insertion;
            if (oldIndex == newIndex) return;
            ShortcutList.Move(oldIndex, newIndex);
            displayedPreset.SwapOrder(oldIndex, newIndex);
            displayedTracksVersion = displayedPreset.TracksVersion;
            displayedPreset.Save();
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


        public void RefreshCurrentAudioPreset()
        {
            var preset = GlobalData.Inst.ActiveAudioPreset;
            if (ReferenceEquals(preset, displayedPreset) && displayedTracksVersion == (preset?.TracksVersion ?? 0))
                return;

            var bindings = GlobalData.Inst.ActiveFunctionPreset?.GetBindingsByAudioId(preset?.Config.Name);
            var rows = preset?.AudioList.Select(audio =>
                new AudioTrackItemViewModel(audio, bindings?[audio.Config.Id].FirstOrDefault())).ToArray();
            displayedPreset = preset;
            displayedTracksVersion = preset?.TracksVersion ?? 0;
            recentVersion = preset?.PlayHistoryVersion ?? 0;
            ShortcutList.ReplaceAll(rows ?? Array.Empty<AudioTrackItemViewModel>());
            TopButtonGroupListBox.SelectedIndex = -1;
            SelectAll = false;
        }

        public void RefreshFunctionPresetValues()
        {
            if (displayedPreset == null || displayedPreset != GlobalData.Inst.ActiveAudioPreset
                || displayedTracksVersion != displayedPreset.TracksVersion) return;
            var bindings = GlobalData.Inst.ActiveFunctionPreset?.GetBindingsByAudioId(displayedPreset.Config.Name);
            foreach (var item in ShortcutList)
                item.RefreshFunctionPresetValues(bindings?[item.Data.Config.Id].FirstOrDefault());
        }



        private NativeFileDrop nativeFileDrop;
        private bool syncingPlaybackMode;

        private async void AudioTrackGrid_Loaded(object sender, RoutedEventArgs e)
        {
            SubscribeLibrary();
            GlobalData.Inst.Playback.Changed += RefreshPlaybackMode;
            RefreshPlaybackMode();
            nativeFileDrop = new NativeFileDrop(AudioListBox, ImportDroppedFiles);
            if (Waveform.Peaks == null) await LoadWaveformAsync();
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
            delete.Click += (_, _) => DeleteButton_Click();
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

        private bool CanMoveSelected(int direction) => !IsLibraryFiltered && ShortcutList.Select((item, i) => (item, i))
            .Any(x => x.item.Marked && x.i + direction >= 0 && x.i + direction < ShortcutList.Count
                && !ShortcutList[x.i + direction].Marked);

        private void MoveSelected(int direction)
        {
            if (IsLibraryFiltered) return;
            var indexes = Enumerable.Range(0, ShortcutList.Count);
            if (direction > 0) indexes = indexes.Reverse();
            foreach (int i in indexes)
            {
                int next = i + direction;
                if (!ShortcutList[i].Marked || next < 0 || next >= ShortcutList.Count || ShortcutList[next].Marked) continue;
                ShortcutList.Move(i, next);
                GlobalData.Inst.ActiveAudioPreset.SwapOrder(i, next);
            }
            displayedTracksVersion = displayedPreset.TracksVersion;
            GlobalData.Inst.ActiveAudioPreset.Save();
        }

        private void RenameTrack(AudioTrackItemViewModel target)
        {
            var window = new RenameAudioWindow(target.Data) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true)
            {
                target.Header = target.Data.Config.Name;
                RefreshFilteredTracks();
            }
        }

        public void Leave()
        {
            GlobalData.Inst.ActiveAudioPreset?.Save();
        }

        private void AudioTrackGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeLibrary();
            nativeFileDrop?.Dispose();
            nativeFileDrop = null;
            GlobalData.Inst.Playback.Changed -= RefreshPlaybackMode;
            addAudioCancellation?.Cancel();
            Leave();
        }
    }
}
