using Microsoft.Win32;
using NAudio.Wave;
using PlayVoice.Audio;
using PlayVoice.Diagnostics;
using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Language;
using Steamworks;
using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PlayVoice.Pages.Preset;

public partial class UploadPresetPage
{
    private const int WorkshopMetadataMaxBytes = 5000; // Steam API limit, UTF-8 bytes.
    private const string TrackDragFormat = "PlayVoice.WorkshopEditTrack";
    private static readonly HashSet<string> SupportedEditAudioFormats = new(
        new[] { ".mp3", ".wav", ".m4a", ".flac", ".aac" }, StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<WorkshopEditTrack> editTracks = new();
    private Item? editedItem;
    private ResourceDataConfig editedResource;
    private CancellationTokenSource editCancellation;
    private string editWorkDirectory;
    private string editDirectory;
    private string originalEditState;
    private string originalCoverPath;
    private BitmapImage originalCoverImage;
    private string originalDescription;
    private string[] originalTags = Array.Empty<string>();
    private string[] originalTrackOrder = Array.Empty<string>();
    private string replacementCoverPath;
    private string[] otherEditTags = Array.Empty<string>();
    private HashSet<string> originalTrackNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> replacedTrackNames = new(StringComparer.OrdinalIgnoreCase);
    private bool editBusy;
    private bool editClosing;
    private bool editLoaded;
    private bool editPublishing;
    private Point editDragStart;
    private WorkshopEditTrack draggedEditTrack;
    private NativeFileDrop editFileDrop;

    public event Action<bool> WorkshopEditClosed;

    public async Task OpenWorkshopEditAsync(Item item)
    {
        OuterLayerBorder.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        EditTrackList.ItemsSource = editTracks;
        EditTitle.Text = item.Title;
        editedItem = item;
        editCancellation = new CancellationTokenSource();
        var token = editCancellation.Token;
        SetEditBusy(true);
        try
        {
            EditStatus.Text = L("正在加载作品");
            var latest = await QueryEditableItemAsync(item.Id);
            token.ThrowIfCancellationRequested();
            editedItem = latest;
            EditTitle.Text = latest.Title;
            if (!latest.IsInstalled || latest.NeedsUpdate)
            {
                EditStatus.Text = L("正在下载原作品");
                using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                downloadTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, GlobalData.Inst.Config.WorkshopDownloadTimeoutSeconds)));
                IProgress<float> progress = new Progress<float>(value => EditProgress.SetProgressInstant(Math.Clamp(value, 0, 1)));
                bool downloaded = await SteamUGC.DownloadAsync(latest.Id, progress.Report, 100, downloadTimeout.Token);
                token.ThrowIfCancellationRequested();
                if (!downloaded || !latest.IsInstalled || latest.NeedsUpdate)
                    throw new IOException(L("原作品下载失败"));
            }

            editWorkDirectory = PresetStorage.CreateWorkshopEditDirectory(latest.Id.Value);
            editDirectory = Path.Combine(editWorkDirectory, "Content");
            EditStatus.Text = L("正在复制文件");
            await JsonTool.CopyDirectoryReplaceTrueAsync(latest.Directory, editDirectory, token);
            token.ThrowIfCancellationRequested();
            if (!JsonTool.LoadJson<ResourceDataConfig>(Path.Combine(editDirectory, "ResourceConfig.json"), out editedResource)
                || editedResource?.Data?.ItemList == null)
                throw new IOException(L("作品资源不完整"));

            editedResource.Title = ResolveWorkshopEditTitle(latest.Title, item.Title, editedResource.Title);
            EditTitle.Text = editedResource.Title;

            foreach (var resource in editedResource.Data.ItemList)
            {
                string path = GetEditAudioPath(resource);
                if (!File.Exists(path)) throw new FileNotFoundException(L("作品资源不完整"), resource.Name);
                if (!originalTrackNames.Add(resource.Name)) throw new IOException(L("作品资源不完整"));
                editTracks.Add(new WorkshopEditTrack(resource));
            }

            // Use the remote description, including support for older plain-text descriptions.
            EditDescription.Text = WorkshopRemoteItemContent.Create(latest).UserDescription;
            var tags = latest.Tags ?? Array.Empty<string>();
            EditTagsListBox.SelectedItems.Clear();
            foreach (WorkshopPage.TabItem<WorkshopPage.TabType> option in EditTagsListBox.ItemsSource)
                if (tags.Contains(WorkshopPage.TabTypeToSteam(option.Type), StringComparer.OrdinalIgnoreCase))
                    EditTagsListBox.SelectedItems.Add(option);
            otherEditTags = tags.Where(tag => !IsKnownEditTag(tag)).ToArray();
            EditOtherTags.Text = string.Join(" / ", otherEditTags);
            string thumbnailFormat = editedResource.ThumbnailFormat;
            if (!string.IsNullOrEmpty(thumbnailFormat)
                && (thumbnailFormat.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || thumbnailFormat.Contains('/') || thumbnailFormat.Contains('\\')))
                throw new IOException(L("作品资源不完整"));
            originalCoverPath = Path.Combine(editDirectory, $"Thumbnail{thumbnailFormat}");
            if (File.Exists(originalCoverPath)) EditCoverImage.Source = LoadEditImage(originalCoverPath);
            else EditCoverImage.Source = await WorkshopPage.DownloadImageAsBitmapAsync(latest, token);
            originalCoverImage = EditCoverImage.Source as BitmapImage;
            token.ThrowIfCancellationRequested();
            originalDescription = EditDescription.Text;
            originalTags = GetEditTags();
            originalTrackOrder = editTracks.Select(track => track.Resource.Name).ToArray();
            // A missing remote title is itself a pending repair, even without other edits.
            originalEditState = string.IsNullOrWhiteSpace(latest.Title) ? null : GetEditState();
            editLoaded = true;
            EditStatus.Text = string.Empty;
            EditProgress.SetProgressInstant(0);
            editFileDrop = new NativeFileDrop(EditTrackList, AddEditFilesAsync);
        }
        catch (OperationCanceledException)
        {
            if (!editClosing) ShowEditError(new IOException(L("原作品下载失败")));
        }
        catch (Exception ex) { ShowEditError(ex); }
        finally
        {
            SetEditBusy(false);
            if (editClosing) FinishWorkshopEdit(false);
        }
    }

    private static async Task<Item> QueryEditableItemAsync(Steamworks.Data.PublishedFileId id)
    {
        if (!SteamClient.IsValid && !MainWindow.Inst.SteamInit())
            throw new InvalidOperationException(L("物品加载失败"));
        var response = await Query.Items.WithFileId(id).AllowCachedResponse(0)
            .WithMetadata(true).WithLongDescription(true).InLanguage(GetWorkshopUploadLanguage()).GetPageAsync(1);
        if (!response.HasValue) throw new IOException(L("物品加载失败"));
        try
        {
            var entries = response.Value.Entries.ToArray();
            if (entries.Length != 1 || entries[0].Id != id || entries[0].Result != Result.OK)
                throw new IOException(L("物品加载失败"));
            if (entries[0].Owner.Id != SteamClient.SteamId)
                throw new InvalidOperationException(L("只能编辑自己的作品"));
            return entries[0];
        }
        finally { response.Value.Dispose(); }
    }

    private string GetEditAudioPath(ResourceDataConfig.ResourceItem resource)
    {
        string name = resource?.Name;
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !SupportedEditAudioFormats.Contains(Path.GetExtension(name)))
            throw new IOException(L("作品资源不完整"));
        return Path.Combine(editDirectory, name);
    }

    private static bool IsKnownEditTag(string tag) =>
        new[] { "music", "sound effect", "voice" }.Contains(tag, StringComparer.OrdinalIgnoreCase);

    private string[] GetEditTags() => otherEditTags
        .Concat(EditTagsListBox.SelectedItems.Cast<WorkshopPage.TabItem<WorkshopPage.TabType>>()
            .OrderBy(option => option.Type).Select(option => WorkshopPage.TabTypeToSteam(option.Type)))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private string GetEditState() => JsonTool.ToJson(new
    {
        Description = EditDescription.Text,
        Tags = GetEditTags(),
        Tracks = editTracks.Select(track => track.Resource.Name).ToArray(),
        Replaced = replacedTrackNames.OrderBy(name => name).ToArray(),
        Cover = replacementCoverPath
    }, false);

    private void EditField_Changed(object sender, RoutedEventArgs e) => UpdateEditSummary();

    private void UpdateEditSummary()
    {
        if (!editLoaded) return;
        UpdateEditSelection();
        var currentNames = editTracks.Select(track => track.Resource.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int added = currentNames.Except(originalTrackNames, StringComparer.OrdinalIgnoreCase).Count();
        int removed = originalTrackNames.Except(currentNames, StringComparer.OrdinalIgnoreCase).Count();
        var changes = new List<string>();
        if (replacementCoverPath != null) changes.Add(L("封面已修改"));
        if (EditDescription.Text != originalDescription) changes.Add(L("简介已修改"));
        if (!GetEditTags().SequenceEqual(originalTags)) changes.Add(L("标签已修改"));
        if (!editTracks.Select(track => track.Resource.Name).Where(originalTrackNames.Contains)
            .SequenceEqual(originalTrackOrder.Where(currentNames.Contains))) changes.Add(L("顺序已调整"));
        EditSummary.Text = string.Format(L("工坊编辑变更摘要"), originalTrackNames.Count, added, removed, editTracks.Count,
            replacedTrackNames.Count, string.Join(" / ", changes));
        EditPublishButton.IsEnabled = !editBusy && editTracks.Count > 0 && GetEditTags().Length > 0
            && GetEditState() != originalEditState;
    }

    private void SetEditBusy(bool busy)
    {
        editBusy = busy;
        EditFields.IsEnabled = !busy && editLoaded;
        EditCancelButton.IsEnabled = !editPublishing;
        EditProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy) EditProgress.SetWaiting("…");
        EditPublishButton.IsEnabled = false;
        UpdateEditSummary();
    }

    private void EditAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox actions || actions.SelectedItem is not ListBoxItem selected) return;
        e.Handled = true;
        actions.SelectedIndex = -1;
        string action = selected.Tag as string;
        if (action == "Cancel")
        {
            EditCancel_Click(sender, e);
            return;
        }
        if (editBusy || !editLoaded) return;
        switch (action)
        {
            case "SelectAll":
                if (EditTrackList.SelectedItems.Count == editTracks.Count) EditTrackList.UnselectAll();
                else EditTrackList.SelectAll();
                break;
            case "Cover": EditCover_Click(sender, e); break;
            case "RestoreCover": EditRestoreCover_Click(sender, e); break;
            case "Add": EditAdd_Click(sender, e); break;
            case "Remove": EditRemove_Click(sender, e); break;
            case "SortAscending": EditSortAscending_Click(sender, e); break;
            case "SortDescending": EditSortDescending_Click(sender, e); break;
            case "Publish": EditPublish_Click(sender, e); break;
        }
    }

    private async void EditAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = L("添加音频"), Multiselect = true,
            Filter = $"{L("音频文件")} (*.mp3;*.wav;*.m4a;*.flac;*.aac)|*.mp3;*.wav;*.m4a;*.flac;*.aac"
        };
        if (dialog.ShowDialog(MainWindow.Inst) == true) await AddEditFilesAsync(dialog.FileNames);
    }

    private async Task AddEditFilesAsync(string[] paths)
    {
        if (editBusy || !editLoaded || editClosing) return;
        SetEditBusy(true);
        try
        {
            string importNotice = null;
            foreach (string source in paths)
            {
                editCancellation.Token.ThrowIfCancellationRequested();
                if (!File.Exists(source) || !SupportedEditAudioFormats.Contains(Path.GetExtension(source)))
                {
                    importNotice = $"{L("已忽略不支持的文件")}: {Path.GetFileName(source)}";
                    continue;
                }
                string name = Path.GetFileName(source);
                int existingIndex = -1;
                for (int i = 0; i < editTracks.Count; i++)
                    if (string.Equals(editTracks[i].Resource.Name, name, StringComparison.OrdinalIgnoreCase)) { existingIndex = i; break; }
                if (existingIndex >= 0 && MessageBox.Show(MainWindow.Inst,
                    string.Format(L("工坊同名歌曲替换提示"), name), L("编辑作品"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    continue;

                EditStatus.Text = $"{L("正在复制文件")}: {name}";
                ResourceDataConfig.ResourceItem resource;
                using (var reader = new AudioFileReader(source))
                    resource = new ResourceDataConfig.ResourceItem
                    {
                        FileName = Path.GetFileNameWithoutExtension(source), FileFormat = Path.GetExtension(source),
                        Duration = reader.TotalTime, Size = new FileInfo(source).Length
                    };
                string destination = existingIndex >= 0 ? GetEditAudioPath(editTracks[existingIndex].Resource) : GetEditAudioPath(resource);
                if (existingIndex >= 0)
                {
                    resource.FileName = editTracks[existingIndex].Resource.FileName;
                    resource.FileFormat = editTracks[existingIndex].Resource.FileFormat;
                }
                string pending = Path.Combine(editDirectory, $"{Guid.NewGuid():N}.pending");
                try
                {
                    await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                    await using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        await input.CopyToAsync(output, editCancellation.Token);
                    File.Move(pending, destination, true);
                }
                finally { if (File.Exists(pending)) File.Delete(pending); }

                var track = new WorkshopEditTrack(resource);
                if (existingIndex >= 0) editTracks[existingIndex] = track;
                else editTracks.Add(track);
                if (originalTrackNames.Contains(resource.Name)) replacedTrackNames.Add(resource.Name);
                UpdateEditSummary();
            }
            EditStatus.Text = importNotice ?? string.Empty;
        }
        catch (OperationCanceledException) when (editClosing) { }
        catch (Exception ex) { ShowEditError(ex); }
        finally
        {
            SetEditBusy(false);
            if (editClosing) FinishWorkshopEdit(false);
        }
    }

    private void EditRemove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var track in EditTrackList.SelectedItems.Cast<WorkshopEditTrack>().ToArray())
            {
                // Only delete the isolated editing copy, never the source preset or Steam installation.
                File.Delete(GetEditAudioPath(track.Resource));
                editTracks.Remove(track);
                replacedTrackNames.Remove(track.Resource.Name);
            }
        }
        catch (Exception ex) { ShowEditError(ex); }
        UpdateEditSummary();
    }

    private void EditSortAscending_Click(object sender, RoutedEventArgs e) => SortEditTracks(false);
    private void EditSortDescending_Click(object sender, RoutedEventArgs e) => SortEditTracks(true);
    private void SortEditTracks(bool descending)
    {
        var sorted = descending
            ? editTracks.OrderByDescending(track => track.Resource.Name, StringComparer.CurrentCultureIgnoreCase).ToArray()
            : editTracks.OrderBy(track => track.Resource.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        for (int i = 0; i < sorted.Length; i++) editTracks.Move(editTracks.IndexOf(sorted[i]), i);
        UpdateEditSummary();
    }

    private void EditTrackSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox selector || selector.SelectedIndex == -1) return;
        e.Handled = true;
        selector.SelectedIndex = -1;
        if (editBusy || !editLoaded || selector.DataContext is not WorkshopEditTrack track) return;
        if (EditTrackList.SelectedItems.Contains(track)) EditTrackList.SelectedItems.Remove(track);
        else EditTrackList.SelectedItems.Add(track);
    }

    private void EditTrackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, EditTrackList)) UpdateEditSelection();
    }

    private void UpdateEditSelection()
    {
        EditSelectAllDot.Visibility = editTracks.Count > 0 && EditTrackList.SelectedItems.Count == editTracks.Count
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EditTrackList_MouseDown(object sender, MouseButtonEventArgs e)
    {
        draggedEditTrack = null;
    }

    private void EditDragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (editBusy || !editLoaded) return;
        editDragStart = e.GetPosition(EditTrackList);
        draggedEditTrack = (sender as FrameworkElement)?.DataContext as WorkshopEditTrack;
        e.Handled = true;
    }

    private void EditTrackList_MouseMove(object sender, MouseEventArgs e)
    {
        if (editBusy || e.LeftButton != MouseButtonState.Pressed || draggedEditTrack == null) return;
        var point = e.GetPosition(EditTrackList);
        if (Math.Abs(point.X - editDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(point.Y - editDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var track = draggedEditTrack;
        draggedEditTrack = null;
        try { DragDrop.DoDragDrop(EditTrackList, new DataObject(TrackDragFormat, track), DragDropEffects.Move); }
        finally { EditInsertionLine.Visibility = Visibility.Collapsed; }
    }

    private void EditTrackList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = editBusy || !editLoaded ? DragDropEffects.None
            : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
            : e.Data.GetDataPresent(TrackDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        EditInsertionLine.Visibility = Visibility.Collapsed;
        if (e.Effects == DragDropEffects.Move && e.Data.GetData(TrackDragFormat) is WorkshopEditTrack track && editTracks.Contains(track))
        {
            var point = e.GetPosition(EditTrackList);
            var scroll = EditTrackList.Template.FindName("PART_ScrollViewer", EditTrackList) as ScrollViewer;
            if (point.Y < SystemParameters.VerticalScrollBarButtonHeight) scroll?.LineUp();
            else if (point.Y > EditTrackList.ActualHeight - SystemParameters.VerticalScrollBarButtonHeight) scroll?.LineDown();
            ShowEditInsertionLine(GetEditDropIndex(point.Y));
        }
        e.Handled = true;
    }

    private int GetEditDropIndex(double y)
    {
        int insertionIndex = editTracks.Count;
        for (int i = 0; i < editTracks.Count; i++)
        {
            if (EditTrackList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem row) continue;
            double top = row.TranslatePoint(new Point(), EditTrackList).Y;
            if (y < top + row.ActualHeight / 2) return i;
            insertionIndex = i + 1;
        }
        return insertionIndex;
    }

    private void ShowEditInsertionLine(int index)
    {
        if (editTracks.Count == 0) return;
        if (EditTrackList.ItemContainerGenerator.ContainerFromIndex(Math.Min(index, editTracks.Count - 1)) is not ListBoxItem row) return;
        double top = row.TranslatePoint(new Point(), EditDragOverlay).Y;
        if (index == editTracks.Count) top += row.ActualHeight;
        Canvas.SetTop(EditInsertionLine, Math.Clamp(top - 1.5, 0, Math.Max(0, EditTrackList.ActualHeight - 3)));
        EditInsertionLine.Width = row.ActualWidth;
        EditInsertionLine.Visibility = Visibility.Visible;
    }

    private void EditTrackList_DragLeave(object sender, DragEventArgs e) => EditInsertionLine.Visibility = Visibility.Collapsed;

    private async void EditTrackList_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        EditInsertionLine.Visibility = Visibility.Collapsed;
        if (editBusy || !editLoaded) return;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            await AddEditFilesAsync((string[])e.Data.GetData(DataFormats.FileDrop));
            return;
        }
        if (e.Data.GetData(TrackDragFormat) is not WorkshopEditTrack track || !editTracks.Contains(track)) return;
        MoveEditTrack(track, GetEditDropIndex(e.GetPosition(EditTrackList).Y));
    }

    private void MoveEditTrack(WorkshopEditTrack track, int insertionIndex)
    {
        int oldIndex = editTracks.IndexOf(track);
        if (oldIndex < 0) return;
        int newIndex = Math.Clamp(insertionIndex > oldIndex ? insertionIndex - 1 : insertionIndex, 0, editTracks.Count - 1);
        if (oldIndex != newIndex) editTracks.Move(oldIndex, newIndex);
        UpdateEditSummary();
    }

    private void EditCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = L("选择预览图"), Filter = $"{L("图片文件")} (*.jpg;*.png;*.gif)|*.jpg;*.png;*.gif" };
        if (dialog.ShowDialog(MainWindow.Inst) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > GlobalData.Inst.Config.WorkshopPreviewMaxBytes)
                throw new IOException(L("预览图要求"));
            var bitmap = LoadEditImage(dialog.FileName);
            if (bitmap == null || Math.Abs(bitmap.PixelWidth - bitmap.PixelHeight) > GlobalData.Inst.Config.WorkshopPreviewSquareTolerance)
                throw new IOException(L("预览图要求"));
            string path = Path.Combine(editWorkDirectory, $"Cover{Path.GetExtension(dialog.FileName).ToLowerInvariant()}");
            File.Copy(dialog.FileName, path, true);
            if (replacementCoverPath != null && replacementCoverPath != path) File.Delete(replacementCoverPath);
            replacementCoverPath = path;
            EditCoverImage.Source = bitmap;
            UpdateEditSummary();
        }
        catch (Exception ex) { ShowEditError(ex); }
    }

    private void EditRestoreCover_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (replacementCoverPath != null) File.Delete(replacementCoverPath);
            replacementCoverPath = null;
            EditCoverImage.Source = originalCoverImage;
            UpdateEditSummary();
        }
        catch (Exception ex) { ShowEditError(ex); }
    }

    private static BitmapImage LoadEditImage(string path)
    {
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private async void EditPublish_Click(object sender, RoutedEventArgs e)
    {
        if (editBusy || !editLoaded || !editedItem.HasValue || GetEditState() == originalEditState) return;
        SetEditBusy(true);
        bool published = false;
        try
        {
            var token = editCancellation.Token;
            EditStatus.Text = L("正在校验作品");
            var latest = await QueryEditableItemAsync(editedItem.Value.Id);
            token.ThrowIfCancellationRequested();
            if (latest.Updated != editedItem.Value.Updated)
                throw new InvalidOperationException(L("作品已更新请重新加载"));
            if (editTracks.Count == 0) throw new InvalidOperationException(L("至少保留一首歌曲"));
            var tags = GetEditTags();
            if (tags.Length == 0) throw new InvalidOperationException(L("至少选择一个标签"));
            if (!GlobalData.Inst.Config.AcceptedUserGeneratedContentAgreement)
            {
                if (new UserGeneratedContentAgreementWindow { Owner = MainWindow.Inst }.ShowDialog() != true) return;
                GlobalData.Inst.Config.AcceptedUserGeneratedContentAgreement = true;
                GlobalData.Inst.Config.Save();
            }

            editedResource.Title = ResolveWorkshopEditTitle(latest.Title, editedResource.Title);
            var content = await PrepareWorkshopEditAsync(token);
            var editor = CreateWorkshopUpdateEditor(latest, content.ContentPath, content.Description,
                content.Metadata, GetWorkshopUploadLanguage());
            foreach (string tag in tags) editor = editor.WithTag(tag);
            if (content.PreviewPath != null) editor = editor.WithPreviewFile(content.PreviewPath);
            token.ThrowIfCancellationRequested();
            editPublishing = true;
            EditCancelButton.IsEnabled = false;
            EditProgress.SetProgressInstant(0);
            MainWindow.Inst.SetBodyInteractionBlocked(true);
            EditStatus.Text = L("正在发布更新");
            isReportingSteamUploadProgress = true;
            var result = await editor.SubmitAsync(this);
            isReportingSteamUploadProgress = false;
            if (!result.Success) throw new IOException($"{L("上传Steam失败")}: {result.Result}");
            published = true;
            if (result.NeedsWorkshopAgreement) SteamFriends.OpenWebOverlay(latest.Url);
            MainWindow.Inst.AddNotification(() => L("通知"), () => L("作品更新成功"), LabelStatus.Success, 4);
        }
        catch (OperationCanceledException) when (editClosing) { }
        catch (Exception ex) { ShowEditError(ex); }
        finally
        {
            isReportingSteamUploadProgress = false;
            if (editPublishing) MainWindow.Inst.SetBodyInteractionBlocked(false);
            editPublishing = false;
            SetEditBusy(false);
            if (published || editClosing) FinishWorkshopEdit(published);
        }
    }

    private static string ResolveWorkshopEditTitle(params string[] candidates) =>
        candidates.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title))
        ?? throw new InvalidOperationException(L("没有输入资源名称"));

    private Editor CreateWorkshopUpdateEditor(Item item, string contentPath, string description, string metadata, string language)
    {
        // Title and description must both be supplied for the update language.
        // Keep the same published file and leave visibility unchanged.
        return item.Edit().WithTitle(ResolveWorkshopEditTitle(item.Title, editedResource.Title))
            .WithContent(contentPath).WithDescription(description).InLanguage(language).WithMetaData(metadata);
    }

    private async Task<(string ContentPath, string Metadata, string Description, string PreviewPath)> PrepareWorkshopEditAsync(CancellationToken token)
    {
        if (editTracks.Count == 0) throw new InvalidOperationException(L("至少保留一首歌曲"));
        foreach (var track in editTracks)
        {
            token.ThrowIfCancellationRequested();
            string path = GetEditAudioPath(track.Resource);
            using var reader = new AudioFileReader(path);
            track.Resource.Duration = reader.TotalTime;
            track.Resource.Size = new FileInfo(path).Length;
        }
        editedResource.Data.ItemList = editTracks.Select(track => track.Resource).ToList();
        editedResource.Data.AudioTotalSize = editTracks.Sum(track => track.Resource.Size);
        editedResource.Data.ExtendedExplanationTotalSize = PresetStorage.GetDirectorySize(Path.Combine(editDirectory, "ExtendedExplanation"));
        editedResource.Description = EditDescription.Text;
        string metadata = JsonTool.ToJson(editedResource.Data, false);
        if (Encoding.UTF8.GetByteCount(metadata) >= WorkshopMetadataMaxBytes)
            throw new InvalidOperationException(L("工坊元数据超限"));

        string previewPath = null;
        string publishDirectory = Path.Combine(editWorkDirectory, "Publish");
        EditStatus.Text = L("正在复制文件");
        await JsonTool.CopyDirectoryReplaceTrueAsync(editDirectory, publishDirectory, token);
        var publishedResource = new ResourceDataConfig
        {
            Title = editedResource.Title, Description = editedResource.Description,
            Data = editedResource.Data, ThumbnailFormat = editedResource.ThumbnailFormat
        };
        if (replacementCoverPath != null)
        {
            publishedResource.ThumbnailFormat = Path.GetExtension(replacementCoverPath);
            previewPath = Path.Combine(publishDirectory, $"Thumbnail{publishedResource.ThumbnailFormat}");
            File.Copy(replacementCoverPath, previewPath, true);
            if (!string.Equals(publishedResource.ThumbnailFormat, editedResource.ThumbnailFormat, StringComparison.OrdinalIgnoreCase))
                File.Delete(Path.Combine(publishDirectory, $"Thumbnail{editedResource.ThumbnailFormat}"));
        }
        else if (!File.Exists(originalCoverPath) && originalCoverImage != null)
        {
            // Preserve a remote-only cover in the downloaded resource package as well.
            publishedResource.ThumbnailFormat = ".png";
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(originalCoverImage));
            using var stream = File.Create(Path.Combine(publishDirectory, "Thumbnail.png"));
            encoder.Save(stream);
        }
        if (!JsonTool.SaveJsonAtomically(Path.Combine(publishDirectory, "ResourceConfig.json"), publishedResource))
            throw new IOException(L("保存失败"));
        string description = JsonTool.ToJson(new WorkshopDescriptionModel
        {
            UserDescription = EditDescription.Text,
            AudioNames = editTracks.Select(track => track.Resource.FileName).ToArray()
        });

        return (publishDirectory, metadata, description, previewPath);
    }

    private void EditCancel_Click(object sender, RoutedEventArgs e)
    {
        if (editPublishing) return;
        editClosing = true;
        editCancellation?.Cancel();
        if (!editBusy) FinishWorkshopEdit(false);
    }

    private void WorkshopEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        if (editCancellation == null || editPublishing) return;
        editClosing = true;
        editCancellation.Cancel();
        if (!editBusy) FinishWorkshopEdit(false);
    }

    private void FinishWorkshopEdit(bool published)
    {
        if (editCancellation == null) return;
        editLoaded = false;
        editFileDrop?.Dispose();
        editFileDrop = null;
        editCancellation.Dispose();
        editCancellation = null;
        try { PresetStorage.DeleteWorkshopEditDirectory(editWorkDirectory); }
        catch (Exception ex) { AppLog.Warning(ex, "清理工坊编辑副本失败。", "Workshop.EditCleanup"); }
        WorkshopEditClosed?.Invoke(published);
    }

    private void ShowEditError(Exception ex)
    {
        AppLog.Error(ex, "编辑创意工坊作品失败。", "Workshop.Edit");
        EditStatus.Text = ex.Message;
        MainWindow.Inst.AddNotification(() => L("通知"), () => ex.Message, LabelStatus.Error, 6);
    }

    private static string L(string key) => LanguageManager.Inst.GetString(key);

    private sealed class WorkshopEditTrack
    {
        public ResourceDataConfig.ResourceItem Resource { get; }
        public string DurationText => AudioData.DurationToString(Resource.Duration);
        public string SizeText => AudioData.SizeToString(Resource.Size);
        public WorkshopEditTrack(ResourceDataConfig.ResourceItem resource) => Resource = resource;
    }
}
