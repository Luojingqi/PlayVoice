using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlayVoice.Audio;
using System.Collections.ObjectModel;
using System.IO;

namespace PlayVoice.Pages.Preset;

public partial class AudioLibraryViewModel : ObservableObject
{
    private readonly ObservableCollection<AudioTrackItemViewModel> tracks;
    private readonly Action<Exception> reportError;
    private readonly Action refresh;
    [ObservableProperty] private AudioTrackItemViewModel selected;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private string tagsText = "";
    public bool HasSelection => Selected != null;
    public string SelectedName => Selected?.Header ?? "";

    public AudioLibraryViewModel(ObservableCollection<AudioTrackItemViewModel> tracks, Action<Exception> reportError, Action refresh)
    {
        this.tracks = tracks;
        this.reportError = reportError;
        this.refresh = refresh;
    }

    partial void OnSelectedChanged(AudioTrackItemViewModel value)
    {
        IsFavorite = value?.Data.Config.IsFavorite ?? false;
        TagsText = string.Join(", ", value?.Data.Config.Tags ?? new());
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedName));
    }

    [RelayCommand]
    private void SaveSelected()
    {
        if (Selected == null) return;
        var preset = Selected.Data.AudioPreset;
        var targets = tracks.Where(item => item.Marked && item.Data.AudioPreset == preset).ToArray();
        if (targets.Length == 0) targets = new[] { Selected };
        var snapshots = targets.Select(item => (item.Data.Config, item.Data.Config.IsFavorite, item.Data.Config.Tags)).ToArray();
        try
        {
            foreach (var item in targets)
            {
                item.Data.Config.IsFavorite = IsFavorite;
                item.Data.Config.Tags = AudioLibraryFilter.ParseTags(TagsText);
            }
            if (!preset.Save()) throw new IOException(Resources.Language.LanguageManager.Inst.GetString("保存失败"));
        }
        catch (Exception exception)
        {
            foreach (var (config, favorite, tags) in snapshots) { config.IsFavorite = favorite; config.Tags = tags; }
            reportError(exception);
        }
        refresh();
    }
}
