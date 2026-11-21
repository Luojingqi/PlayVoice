using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PlayVoice.Audio;
using PlayVoice.Resources.Language;

namespace PlayVoice.Pages.Preset;

public partial class AudioTrackGrid
{
    public ICollectionView AudioView { get; private set; }
    public AudioLibraryViewModel Library { get; private set; }
    private bool IsLibraryFiltered => !string.IsNullOrWhiteSpace(LibrarySearch.Text)
        || FavoritesFilter.IsChecked == true || RecentFilter.IsChecked == true;

    private void InitializeLibrary()
    {
        Library = new AudioLibraryViewModel(ShortcutList, ShowLibraryError, RefreshFilteredTracks, StopPreview);
        AudioView = CollectionViewSource.GetDefaultView(ShortcutList);
        AudioView.Filter = item => item is AudioTrackItemViewModel track
            && AudioLibraryFilter.Matches(track.Data.Config, LibrarySearch.Text,
                FavoritesFilter.IsChecked == true, RecentFilter.IsChecked == true);
        AudioView.CollectionChanged += (_, _) => UpdateEmptyLibrary();
        UpdateEmptyLibrary();
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
        if (IsLibraryFiltered) AudioView.Refresh();
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
        if (e.PropertyName == nameof(Library.Selected)) await LoadWaveformAsync();
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
