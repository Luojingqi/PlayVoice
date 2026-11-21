using CommunityToolkit.Mvvm.ComponentModel;
using PlayVoice.Audio;
using PlayVoice.Resources.Language;
using System.Collections.ObjectModel;
namespace PlayVoice;

public partial class ShellViewModel : ObservableObject
{
    public LanguageManager L => LanguageManager.Inst;
    [ObservableProperty] private IReadOnlyList<TrackRow> tracks = Array.Empty<TrackRow>();
    public ObservableCollection<string> AudioPresetNames { get; } = new();
    public ObservableCollection<string> GamePresetNames { get; } = new();
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool favorites;
    [ObservableProperty] private bool recent;
    [ObservableProperty] private string pageTitle = "PlayVoice";
    [ObservableProperty] private string notice = "";
    [ObservableProperty] private bool hasNotice;
    [ObservableProperty] private string requirements = "";
    [ObservableProperty] private string selectedName = "";
    [ObservableProperty] private string nowPlaying = "";
    [ObservableProperty] private string playbackStatus = "";
    [ObservableProperty] private string outputDevice = "";
    [ObservableProperty] private double progress;
    [ObservableProperty] private bool isEmpty = true;
    [ObservableProperty] private string activeAudioPresetName;
    [ObservableProperty] private string activeGamePresetName;
    [ObservableProperty] private string steamStatus = "";
    [ObservableProperty] private string stopHotkey = "";
    [ObservableProperty] private string presetSummary = "";
    partial void OnSearchChanged(string value) => Filter();
    partial void OnFavoritesChanged(bool value) => Filter();
    partial void OnRecentChanged(bool value) => Filter();
    public void Filter()
    {
        Tracks = GlobalData.Inst?.ActiveAudioPreset?.AudioList
            .Where(audio => AudioLibraryFilter.Matches(audio.Config, Search, Favorites, Recent))
            .Select(audio => new TrackRow(audio)).ToArray() ?? Array.Empty<TrackRow>();
        IsEmpty = Tracks.Count == 0;
    }
    public void RefreshLanguage() { OnPropertyChanged(nameof(L)); Filter(); }
}
public sealed class TrackRow(AudioData audio)
{
    public AudioData Audio { get; } = audio;
    public string Name => Audio.Config.FileName;
    public string Detail => string.Join(" · ", Audio.Config.Tags ?? []) + $"  {Audio.AudioTrackArray[0].TotalTime:mm\\:ss}";
    public string Hotkey => GlobalData.Inst.GetAudioHotkey(Audio, false)?.ToString() ?? "";
}
