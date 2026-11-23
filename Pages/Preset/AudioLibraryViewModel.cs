using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlayVoice.Audio;
using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Resources.Language;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace PlayVoice.Pages.Preset;

public partial class AudioLibraryViewModel : ObservableObject
{
    private readonly ObservableCollection<AudioTrackItemViewModel> tracks;
    private readonly Action<Exception> reportError;
    private readonly Action refresh;
    private readonly Action stopPreview;
    private readonly Dictionary<string, List<UndoStep>> history = new();
    private sealed record UndoStep(Func<AudioTrackItemViewModel, bool> Restore, FunctionPresetData GamePreset = null);
    private (AudioTrackItemViewModel Track, FunctionPresetData Preset)? volumeEdit;
    private double volume = 100;
    public event Action SelectionChanging;
    public event Action EditValuesReloaded;
    public event Action TagsReloaded;
    [ObservableProperty] private AudioTrackItemViewModel selected;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private string tagsText = "";
    [ObservableProperty] private double selectionStart;
    [ObservableProperty] private double selectionEnd;
    [ObservableProperty] private double fadeIn;
    [ObservableProperty] private double fadeOut;
    [ObservableProperty] private string editError = "";
    public double OriginalDuration => Selected?.Data.AudioTrackArray[0]?.TotalTime.TotalSeconds ?? 0;
    public bool HasSelection => Selected != null;
    public string SelectedName => Selected?.Header ?? "";

    public AudioLibraryViewModel(ObservableCollection<AudioTrackItemViewModel> tracks, Action<Exception> reportError, Action refresh, Action stopPreview)
    {
        this.tracks = tracks;
        this.reportError = reportError;
        this.refresh = refresh;
        this.stopPreview = stopPreview;
    }

    partial void OnSelectedChanging(AudioTrackItemViewModel oldValue, AudioTrackItemViewModel newValue)
    {
        SelectionChanging?.Invoke();
        if (oldValue != null) oldValue.PropertyChanged -= SelectedTrackChanged;
    }

    partial void OnSelectedChanged(AudioTrackItemViewModel value)
    {
        if (value != null) value.PropertyChanged += SelectedTrackChanged;
        IsFavorite = value?.Data.Config.IsFavorite ?? false;
        LoadTags();
        LoadEditValues();
        RefreshVolume();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(OriginalDuration));
    }

    private void LoadTags()
    {
        TagsText = string.Join(", ", Selected?.Data.Config.Tags ?? new());
        TagsReloaded?.Invoke();
    }

    public void DiscardDrafts()
    {
        LoadTags();
        LoadEditValues();
    }

    private void LoadEditValues()
    {
        EditError = "";
        var edit = Selected?.Data.Config.Edit;
        SelectionStart = edit?.StartSeconds ?? 0;
        SelectionEnd = edit?.EndSeconds ?? OriginalDuration;
        FadeIn = edit?.FadeInSeconds ?? 0;
        FadeOut = edit?.FadeOutSeconds ?? 0;
        EditValuesReloaded?.Invoke();
    }

    public AudioEditSettings PendingEdit()
    {
        var edit = new AudioEditSettings { StartSeconds = SelectionStart, EndSeconds = SelectionEnd, FadeInSeconds = FadeIn, FadeOutSeconds = FadeOut };
        edit.Validate(OriginalDuration, Selected.Data.AudioTrackArray[0].WaveFormat.SampleRate);
        return edit;
    }

    [RelayCommand]
    private void ResetAudioEdit()
    {
        if (Selected != null && SaveClip(Selected, null)) LoadEditValues();
    }

    public bool CommitClip()
    {
        if (Selected == null) return false;
        AudioEditSettings next;
        try { next = PendingEdit(); }
        catch (ArgumentException)
        {
            EditError = LanguageManager.Inst.GetString("选区或淡化时长无效");
            return false;
        }

        bool saved = SaveClip(Selected, next);
        EditError = saved ? "" : LanguageManager.Inst.GetString("保存失败");
        return saved;
    }

    private bool SaveClip(AudioTrackItemViewModel track, AudioEditSettings next, bool remember = true)
    {
        var config = track.Data.Config;
        var previous = config.Edit;
        var originalDuration = track.Data.AudioTrackArray[0].TotalTime.TotalSeconds;
        bool effectiveMatch = (previous?.StartSeconds ?? 0) == (next?.StartSeconds ?? 0)
            && (previous?.EndSeconds ?? originalDuration) == (next?.EndSeconds ?? originalDuration)
            && (previous?.FadeInSeconds ?? 0) == (next?.FadeInSeconds ?? 0)
            && (previous?.FadeOutSeconds ?? 0) == (next?.FadeOutSeconds ?? 0);
        if (effectiveMatch && (remember && next != null || (previous == null) == (next == null))) return true;

        config.Edit = next;
        if (!TrySave(track.Data.AudioPreset.Save, () => config.Edit = previous)) return false;
        stopPreview();
        track.Data.Stop();
        track.Duration = track.Data.PlaybackDuration;
        if (remember) Remember(track, new UndoStep(target =>
        {
            if (!SaveClip(target, previous, remember: false)) return false;
            LoadEditValues();
            return true;
        }));
        return true;
    }

    public void CommitFavorite(bool favorite)
    {
        if (Selected == null) return;
        SaveMetadata(new[] { Selected }, favorite, null);
        if (IsFavorite == Selected.Data.Config.IsFavorite) OnPropertyChanged(nameof(IsFavorite));
        else IsFavorite = Selected.Data.Config.IsFavorite;
    }

    public void CommitTags()
    {
        if (Selected == null) return;
        SaveMetadata(new[] { Selected }, null, AudioLibraryFilter.ParseTags(TagsText));
        LoadTags();
    }

    public bool CanApplyMetadataToMarked => Selected != null && tracks.Any(item => item.Marked
        && ReferenceEquals(item.Data.AudioPreset, Selected.Data.AudioPreset));

    public void ApplyMetadataToMarked()
    {
        if (Selected == null) return;
        var preset = Selected.Data.AudioPreset;
        var source = Selected.Data.Config;
        SaveMetadata(tracks.Where(item => item.Marked && ReferenceEquals(item.Data.AudioPreset, preset)),
            source.IsFavorite, source.Tags ?? new());
    }

    private void SaveMetadata(IEnumerable<AudioTrackItemViewModel> targets, bool? favorite, List<string> tags)
    {
        var changes = targets.Select(track =>
        {
            var config = track.Data.Config;
            return (Track: track, Favorite: config.IsFavorite, Tags: config.Tags,
                ChangeFavorite: favorite.HasValue && favorite.Value != config.IsFavorite,
                ChangeTags: tags != null && !(config.Tags ?? new()).SequenceEqual(tags));
        }).Where(change => change.ChangeFavorite || change.ChangeTags).ToArray();
        if (changes.Length == 0) return;

        foreach (var change in changes)
        {
            if (change.ChangeFavorite) change.Track.Data.Config.IsFavorite = favorite.GetValueOrDefault();
            if (change.ChangeTags) change.Track.Data.Config.Tags = new(tags);
        }

        if (!TrySave(changes[0].Track.Data.AudioPreset.Save, () =>
        {
            foreach (var change in changes)
            {
                if (change.ChangeFavorite) change.Track.Data.Config.IsFavorite = change.Favorite;
                if (change.ChangeTags) change.Track.Data.Config.Tags = change.Tags;
            }
        })) return;

        foreach (var change in changes)
            Remember(change.Track, new UndoStep(track => RestoreMetadata(track,
                change.ChangeFavorite ? change.Favorite : null, change.ChangeTags, change.Tags)));
        refresh();
    }

    private bool RestoreMetadata(AudioTrackItemViewModel track, bool? favorite, bool restoreTags, List<string> tags)
    {
        var config = track.Data.Config;
        bool previousFavorite = config.IsFavorite;
        var previousTags = config.Tags;
        if (favorite.HasValue) config.IsFavorite = favorite.Value;
        if (restoreTags) config.Tags = tags;
        if (!TrySave(track.Data.AudioPreset.Save, () =>
        {
            config.IsFavorite = previousFavorite;
            config.Tags = previousTags;
        })) return false;

        if (favorite.HasValue) IsFavorite = config.IsFavorite;
        if (restoreTags) LoadTags();
        refresh();
        return true;
    }

    public double Volume
    {
        get => volume;
        set
        {
            if (!SetProperty(ref volume, value) || Selected == null) return;
            var track = Selected;
            double decibel = AudioData.ProportionToDecibel(value / 100);
            track.Data.SetVolume(decibel);
            if (volumeEdit == null)
                SaveVolume(track, GlobalData.Inst.ActiveFunctionPreset, decibel);
        }
    }

    public void BeginVolumeEdit()
    {
        if (volumeEdit == null && Selected != null && GlobalData.Inst.ActiveFunctionPreset is { } preset)
            volumeEdit = (Selected, preset);
    }

    public void FinishVolumeEdit()
    {
        if (volumeEdit is not { } edit) return;
        volumeEdit = null;
        SaveVolume(edit.Track, edit.Preset, AudioData.ProportionToDecibel(Volume / 100));
    }

    public void CancelVolumeEdit()
    {
        if (volumeEdit is not { } edit) return;
        volumeEdit = null;
        var track = edit.Track;
        if (track.Data.AudioPreset.AudioList.Contains(track.Data))
            track.Data.SetVolume(GlobalData.Inst.GetAudioDecibel(track.Data));
        RefreshVolume();
    }

    public void RefreshVolume()
    {
        if (volumeEdit == null)
            SetProperty(ref volume, Selected?.VolumeSliderValue ?? 100, nameof(Volume));
        UndoEditCommand.NotifyCanExecuteChanged();
    }

    private void SelectedTrackChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioTrackItemViewModel.VolumeSliderValue)) RefreshVolume();
    }

    private bool SaveVolume(AudioTrackItemViewModel track, FunctionPresetData preset, double next, bool remember = true)
    {
        if (preset == null) { RefreshVolume(); return false; }
        string name = track.Data.AudioPreset.Config.Name;
        string id = track.Data.Config.Id;
        double previous = preset.GetAudioDecibel(name, id);
        next = Math.Clamp(next, AudioData.MinDecibel, AudioData.MaxDecibel);
        if (Math.Abs(next - previous) < 1e-8) return true;

        preset.SetAudioDecibel(name, id, next);
        bool saved = TrySave(preset.Save, () => preset.SetAudioDecibel(name, id, previous));
        if (ReferenceEquals(preset, GlobalData.Inst.ActiveFunctionPreset))
        {
            double decibel = saved ? next : previous;
            track.Data.SetVolume(decibel);
            track.RefreshVolumeSlider(decibel);
            RefreshVolume();
        }
        if (saved && remember)
            Remember(track, new UndoStep(target => SaveVolume(target, preset, previous, remember: false), preset));
        return saved;
    }

    private bool TrySave(Func<bool> save, Action rollback)
    {
        try
        {
            if (save()) return true;
            throw new IOException(LanguageManager.Inst.GetString("保存失败"));
        }
        catch (Exception exception)
        {
            rollback();
            reportError(exception);
            return false;
        }
    }

    private void Remember(AudioTrackItemViewModel track, UndoStep step)
    {
        if (!history.TryGetValue(track.Data.Config.Id, out var steps))
            history.Add(track.Data.Config.Id, steps = new());
        steps.Add(step);
        UndoEditCommand.NotifyCanExecuteChanged();
    }

    private static int UndoIndex(List<UndoStep> steps) => steps.FindLastIndex(step => step.GamePreset == null
        || ReferenceEquals(step.GamePreset, GlobalData.Inst.ActiveFunctionPreset));

    private bool CanUndoEdit() => Selected != null && history.TryGetValue(Selected.Data.Config.Id, out var steps)
        && UndoIndex(steps) >= 0;

    [RelayCommand(CanExecute = nameof(CanUndoEdit))]
    private void UndoEdit()
    {
        if (Selected == null || !history.TryGetValue(Selected.Data.Config.Id, out var steps)) return;
        int index = UndoIndex(steps);
        if (index < 0 || !steps[index].Restore(Selected)) return;
        steps.RemoveAt(index);
        UndoEditCommand.NotifyCanExecuteChanged();
    }

    public void ClearEditHistory()
    {
        history.Clear();
        UndoEditCommand.NotifyCanExecuteChanged();
    }
}
