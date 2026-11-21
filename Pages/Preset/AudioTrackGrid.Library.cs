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
        Library = new AudioLibraryViewModel(ShortcutList, ShowLibraryError, RefreshLibraryStatus);
        AudioView = CollectionViewSource.GetDefaultView(ShortcutList);
        AudioView.Filter = item => item is AudioTrackItemViewModel track
            && AudioLibraryFilter.Matches(track.Data.Config, LibrarySearch.Text,
                FavoritesFilter.IsChecked == true, RecentFilter.IsChecked == true);
        ShortcutList.CollectionChanged += (_, _) => RefreshLibraryStatus();
    }

    private void SubscribeLibrary()
    {
        GlobalData.Inst.Playback.Changed += RefreshLibraryStatus;
        GlobalData.Inst.StartupRequirementsChanged += DeviceStateChanged;
        LanguageManager.Inst.CultureChanged += LibraryLanguageChanged;
        RefreshLibraryStatus();
    }

    private void UnsubscribeLibrary()
    {
        GlobalData.Inst.Playback.Changed -= RefreshLibraryStatus;
        GlobalData.Inst.StartupRequirementsChanged -= DeviceStateChanged;
        LanguageManager.Inst.CultureChanged -= LibraryLanguageChanged;
        StopPreview();
    }

    private void LibraryLanguageChanged(System.Globalization.CultureInfo culture, LanguageManager.LanguageInfo language) => RefreshLibraryStatus();
    private void DeviceStateChanged() { StopPreview(); RefreshLibraryStatus(); }

    private void LibraryFilterChanged(object sender, RoutedEventArgs e)
    {
        if (AudioView == null) return;
        foreach (var track in ShortcutList) track.Marked = false;
        SelectAll = false;
        // Recent ordering reads metadata directly, without changing persisted track order.
        ((ListCollectionView)AudioView).CustomSort = RecentFilter.IsChecked == true ? new RecentComparer() : null;
        AudioView.Refresh();
        RefreshLibraryStatus();
    }

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
        AudioView.Refresh();
        EmptyLibrary.Visibility = AudioView.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        var global = GlobalData.Inst;
        string unavailable = LanguageManager.Inst.GetString("未绑定");
        var output = global.GetRun() ? global.Equipment.VirtualLoudspeaker : global.Equipment.PhysicalLoudspeaker;
        OutputStatus.Text = $"{LanguageManager.Inst.GetString("输出设备")}: {output?.FriendlyName ?? unavailable}  |  {LanguageManager.Inst.GetString("试听设备")}: {global.Equipment.PhysicalLoudspeaker?.FriendlyName ?? unavailable}";
        PlaybackStatus.Text = LanguageManager.Inst.GetString(global.Playback.IsPlaying ? "正在播放" : global.Playback.IsPaused ? "已暂停" : "已停止");
    }

    private void StopPreview() => GlobalData.Inst.Playback.StopPreview();
    private void PreviewLibraryAudio(object sender, RoutedEventArgs e)
    {
        if (Library.Selected == null) return;
        try { GlobalData.Inst.Playback.Preview(Library.Selected.Data); }
        catch (Exception exception) { ShowLibraryError(exception); }
    }

    private void StopLibraryAudio(object sender, RoutedEventArgs e) { GlobalData.Inst.Playback.StopAll(); StopPreview(); }
    private void TestLibraryRoute(object sender, RoutedEventArgs e)
    {
        if (AudioListBox.SelectedItem is not AudioTrackItemViewModel selected) return;
        StopLibraryAudio(sender, e);
        GlobalData.Inst.Playback.Toggle(selected.Data);
    }

    private void ShowLibraryError(Exception exception)
    {
        Diagnostics.AppLog.Error(exception, "音效库操作失败。", "AudioLibrary");
        MainWindow.Inst.AddNotification(() => LanguageManager.Inst.GetString("通知"),
            () => LanguageManager.Inst.GetString("操作失败"), LabelStatus.Error, 4);
    }
}
