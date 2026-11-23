using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using PlayVoice.Audio;
using PlayVoice.Resources.Language;

namespace PlayVoice.Pages.Preset;

public partial class AudioTrackGrid
{
    public ICollectionView AudioView { get; private set; }
    public AudioLibraryViewModel Library { get; private set; }
    private TextBox activeEditorTextBox;
    private string originalEditorText;
    private bool refreshQueued;
    private AudioTrackItemViewModel editedTrackInFilter;
    private TextBox[] clipInputs;
    private bool IsLibraryFiltered => !string.IsNullOrWhiteSpace(LibrarySearch.Text)
        || FavoritesFilter.IsChecked == true || RecentFilter.IsChecked == true;

    private void InitializeLibrary()
    {
        Library = new AudioLibraryViewModel(ShortcutList, ShowLibraryError, RefreshFilteredTracks, StopPreview);
        clipInputs = new[] { LibraryTrimStart, LibraryTrimEnd, LibraryFadeIn, LibraryFadeOut };
        foreach (var input in clipInputs.Append(LibraryTags))
        {
            input.GotKeyboardFocus += LibraryTextFocused;
            input.LostKeyboardFocus += (_, _) =>
            {
                if (activeEditorTextBox == input) FinishTextEdit();
            };
            input.PreviewKeyDown += LibraryTextKeyDown;
        }
        Library.SelectionChanging += FinishLibraryEdits;
        Library.EditValuesReloaded += () => Array.ForEach(clipInputs, ReloadEditorText);
        Library.TagsReloaded += () => ReloadEditorText(LibraryTags);
        Waveform.EditCompleted += () =>
        {
            if (!CanCommitEditor) return;
            ReloadEditorText(LibraryTrimStart);
            ReloadEditorText(LibraryTrimEnd);
            CommitClipInputs();
        };
        LibraryVolume.EditStarted += Library.BeginVolumeEdit;
        LibraryVolume.EditCompleted += FinishVolumeEdit;
        AudioView = CollectionViewSource.GetDefaultView(ShortcutList);
        AudioView.Filter = item => item is AudioTrackItemViewModel track
            && (ReferenceEquals(track, editedTrackInFilter) || AudioLibraryFilter.Matches(track.Data.Config,
                LibrarySearch.Text, FavoritesFilter.IsChecked == true, RecentFilter.IsChecked == true));
        AudioView.CollectionChanged += (_, _) => UpdateEmptyLibrary();
        UpdateEmptyLibrary();
    }

    private bool CanCommitEditor => Library.Selected is { } track
        && ReferenceEquals(track.Data.AudioPreset, GlobalData.Inst.ActiveAudioPreset)
        && track.Data.AudioPreset.AudioList.Contains(track.Data);

    private void LibraryTextFocused(object sender, KeyboardFocusChangedEventArgs e)
    {
        activeEditorTextBox = (TextBox)sender;
        originalEditorText = activeEditorTextBox.Text;
    }

    private void LibraryTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || activeEditorTextBox != sender) return;
        CommitEditorText(activeEditorTextBox);
        e.Handled = true;
    }

    private void FinishTextEdit()
    {
        var input = activeEditorTextBox;
        activeEditorTextBox = null;
        if (input != null) CommitEditorText(input);
    }

    private void CommitEditorText(TextBox input)
    {
        if (!CanCommitEditor) return;
        if (input.Text == originalEditorText)
        {
            if (input != LibraryTags && !string.IsNullOrEmpty(Library.EditError)) CommitClipInputs();
            return;
        }
        input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ReferenceEquals(input, LibraryTags)) Library.CommitTags();
        else CommitClipInputs();
        originalEditorText = input.Text;
    }

    private void ReloadEditorText(TextBox input)
    {
        if (input.GetBindingExpression(TextBox.TextProperty) is not { } binding) return;
        Validation.ClearInvalid(binding);
        binding.UpdateTarget();
        if (!input.IsKeyboardFocused) return;
        activeEditorTextBox = input;
        originalEditorText = input.Text;
    }

    private void CommitClipInputs()
    {
        if (clipInputs.Any(Validation.GetHasError))
        {
            Library.EditError = LanguageManager.Inst.GetString("请输入有效的秒数");
            return;
        }
        Library.CommitClip();
    }

    private void LibraryFavoriteClicked(object sender, RoutedEventArgs e)
    {
        if (CanCommitEditor) Library.CommitFavorite(((CheckBox)sender).IsChecked == true);
    }

    internal void FinishLibraryEdits()
    {
        FinishTextEdit();
        Waveform.FinishEditing();
        FinishVolumeEdit();
    }

    private void FinishVolumeEdit()
    {
        if (CanCommitEditor) Library.FinishVolumeEdit();
        else Library.CancelVolumeEdit();
    }

    internal void EndLibraryEditSession()
    {
        FinishLibraryEdits();
        Library.ClearEditHistory();
        if (CanCommitEditor) Library.DiscardDrafts();
        if (editedTrackInFilter == null) return;
        editedTrackInFilter = null;
        QueueFilteredRefresh();
    }

    private void SubscribeLibrary()
    {
        Library.PropertyChanged += EditSelectionChanged;
        GlobalData.Inst.Playback.Changed += RefreshLibraryStatus;
        GlobalData.Inst.StartupRequirementsChanged += DeviceStateChanged;
        LanguageManager.Inst.CultureChanged += LibraryLanguageChanged;
        RefreshLibraryStatus();
    }

    private void UnsubscribeLibrary()
    {
        Library.PropertyChanged -= EditSelectionChanged;
        waveformCancellation?.Cancel();
        GlobalData.Inst.Playback.Changed -= RefreshLibraryStatus;
        GlobalData.Inst.StartupRequirementsChanged -= DeviceStateChanged;
        LanguageManager.Inst.CultureChanged -= LibraryLanguageChanged;
        StopPreview();
    }

    private void LibraryLanguageChanged(System.Globalization.CultureInfo culture, LanguageManager.LanguageInfo language)
    {
        RefreshLibraryStatus();
        WaveformStatus.Text = string.IsNullOrEmpty(waveformStatusKey) ? "" : LanguageManager.Inst.GetString(waveformStatusKey);
    }
    private void DeviceStateChanged() { StopPreview(); RefreshLibraryStatus(); }

    private void LibraryFilterChanged(object sender, RoutedEventArgs e)
    {
        if (AudioView == null) return;
        editedTrackInFilter = null;
        foreach (var track in ShortcutList) track.Marked = false;
        SelectAll = false;
        // Recent ordering reads metadata directly, without changing persisted track order.
        var view = (ListCollectionView)AudioView;
        if ((view.CustomSort != null) != (RecentFilter.IsChecked == true))
            view.CustomSort = RecentFilter.IsChecked == true ? new RecentComparer() : null;
        else
            view.Refresh();
    }

    private void RefreshFilteredTracks()
    {
        if (!IsLibraryFiltered) return;
        // Keep the edited row available for undo if its new metadata excludes it from this view.
        editedTrackInFilter = Library.Selected;
        QueueFilteredRefresh();
    }

    private void QueueFilteredRefresh()
    {
        if (refreshQueued) return;
        refreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            refreshQueued = false;
            if (IsLibraryFiltered) AudioView.Refresh();
        }, DispatcherPriority.Background);
    }

    private void UpdateEmptyLibrary() =>
        EmptyLibrary.Visibility = AudioView.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    private sealed class RecentComparer : System.Collections.IComparer
    {
        public int Compare(object x, object y) => Nullable.Compare(
            (y as AudioTrackItemViewModel)?.Data.Config.LastPlayedAt,
            (x as AudioTrackItemViewModel)?.Data.Config.LastPlayedAt);
    }

    private void RefreshLibraryStatus()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(RefreshLibraryStatus); return; }
        if (AudioView == null) return;
        var global = GlobalData.Inst;
        if (RecentFilter.IsChecked == true && displayedPreset != null && displayedPreset == global.ActiveAudioPreset
            && displayedTracksVersion == displayedPreset.TracksVersion && recentVersion != displayedPreset.PlayHistoryVersion)
        {
            recentVersion = displayedPreset.PlayHistoryVersion;
            AudioView.Refresh();
        }
        string unavailable = LanguageManager.Inst.GetString("未绑定");
        var output = global.GetRun() ? global.Equipment.VirtualLoudspeaker : global.Equipment.PhysicalLoudspeaker;
        OutputStatus.Text = $"{LanguageManager.Inst.GetString("输出设备")}: {output?.FriendlyName ?? unavailable}  |  {LanguageManager.Inst.GetString("试听设备")}: {global.Equipment.PhysicalLoudspeaker?.FriendlyName ?? unavailable}";
        PlaybackStatus.Text = LanguageManager.Inst.GetString(global.Playback.IsPlaying ? "正在播放" : global.Playback.IsPaused ? "已暂停" : "已停止");
    }

    private void StopPreview() => GlobalData.Inst.Playback.StopPreview();
    private void PreviewLibraryAudio(object sender, RoutedEventArgs e)
    {
        if (Library.Selected == null) return;
        if (clipInputs.Any(Validation.GetHasError)) return;
        try { GlobalData.Inst.Playback.Preview(Library.Selected.Data, Library.PendingEdit()); }
        catch (Exception exception) { ShowLibraryError(exception); }
    }

    private void StopLibraryAudio(object sender, RoutedEventArgs e) { GlobalData.Inst.Playback.StopAll(); StopPreview(); }
    private void TestLibraryRoute(object sender, RoutedEventArgs e)
    {
        if (AudioListBox.SelectedItem is not AudioTrackItemViewModel selected) return;
        StopLibraryAudio(sender, e);
        GlobalData.Inst.Playback.Toggle(selected.Data);
    }

    internal void ShowLibraryError(Exception exception)
    {
        Diagnostics.AppLog.Error(exception, "音效库操作失败。", "AudioLibrary");
        MainWindow.Inst.AddNotification(() => LanguageManager.Inst.GetString("通知"),
            () => LanguageManager.Inst.GetString("操作失败"), LabelStatus.Error, 4);
    }

    private CancellationTokenSource waveformCancellation;
    private string waveformStatusKey;
    private async void EditSelectionChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Library.Selected)) return;
        if (editedTrackInFilter != null && !ReferenceEquals(editedTrackInFilter, Library.Selected))
        {
            editedTrackInFilter = null;
            QueueFilteredRefresh();
        }
        await LoadWaveformAsync();
    }
    private async Task LoadWaveformAsync()
    {
        waveformCancellation?.Cancel();
        Waveform.Peaks = null;
        waveformStatusKey = null;
        WaveformStatus.Text = "";
        var selected = Library.Selected;
        if (selected == null || selected.Data.AudioPreset != GlobalData.Inst.ActiveAudioPreset) return;
        using var cancellation = new CancellationTokenSource();
        waveformCancellation = cancellation;
        waveformStatusKey = "加载波形";
        WaveformStatus.Text = LanguageManager.Inst.GetString(waveformStatusKey);
        try
        {
            string path = selected.Data.AudioTrackArray[0].FileName;
            int bins = Math.Clamp(GlobalData.Inst.Config.WaveformPeakBins, 16, 4096);
            var token = cancellation.Token;
            var peaks = await Task.Run(() => WaveformPeaks.Read(path, bins, token), token);
            if (token.IsCancellationRequested || !IsLoaded || Library.Selected != selected) return;
            Waveform.Peaks = peaks;
            waveformStatusKey = null;
            WaveformStatus.Text = "";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (cancellation.IsCancellationRequested || Library.Selected != selected || !IsLoaded) return;
            Diagnostics.AppLog.Error(exception, "波形加载失败。", "AudioLibrary.Waveform");
            waveformStatusKey = "波形加载失败";
            WaveformStatus.Text = LanguageManager.Inst.GetString(waveformStatusKey);
        }
        finally { if (ReferenceEquals(waveformCancellation, cancellation)) waveformCancellation = null; }
    }
}
