using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PlayVoice.Audio;
using PlayVoice.Hotkey;
using PlayVoice.Pages;
using PlayVoice.Pages.Preset;
using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using System.IO;
namespace PlayVoice;

public partial class MainWindow : Window
{
    public static MainWindow Inst { get; private set; }
    private readonly ShellViewModel vm = new();
    private bool ready, syncing;
    private AudioData selected;
    private readonly DispatcherTimer timer = new();
    private CancellationTokenSource waveformCancellation = new();
    private long presetRequest;
    public MainWindow()
    {
        Inst = this; InitializeComponent(); DataContext = vm;
        Opened += async (_, _) => await Execute(async () =>
        {
            new GlobalData(); ready = true;
            await GlobalData.Inst.RestoreActiveAudioPresetAsync();
            HotkeyManager.Inst.Start(); RefreshNames(); RefreshSettings(); vm.Filter();
            timer.Interval = TimeSpan.FromMilliseconds(250); timer.Tick += (_, _) => RefreshStatus(); timer.Start();
        });
        Closed += (_, _) => { timer.Stop(); waveformCancellation.Cancel(); if (ready) { GlobalData.Inst.Playback.Reset(); GlobalData.Inst.DisposeAudioPresetForExit(); GlobalData.Inst.AudioProxy.Stop(); GlobalData.Inst.Equipment.Dispose(); GlobalData.Inst.Config.Save(); HotkeyManager.Inst.Stop(); } DesktopApplication.Current.Dispatcher.HasShutdownStarted = true; if (Steamworks.SteamClient.IsValid) Steamworks.SteamClient.Shutdown(); };
    }
    public void AddNotification(Func<string> title, Func<string> text, LabelStatus status = LabelStatus.None, double duration = 4) => Dispatcher.UIThread.Post(() => { vm.Notice = title() + " · " + text(); vm.HasNotice = true; });
    private async Task Execute(Func<Task> action) { try { await action(); } catch (Exception e) { Diagnostics.AppLog.Error(e, "Avalonia operation failed", "Desktop"); vm.Notice = e.Message; vm.HasNotice = true; } }
    private void Act(Action action) { if (ready) _ = Execute(() => { action(); return Task.CompletedTask; }); }
    private void RefreshNames() { syncing = true; vm.AudioPresetNames.Clear(); foreach (var n in AudioPresetDataTool.GetAllAudioPresetName()) vm.AudioPresetNames.Add(n); vm.GamePresetNames.Clear(); foreach (var n in FunctionPresetDataTool.GetAll()) vm.GamePresetNames.Add(n.Name); vm.ActiveAudioPresetName = GlobalData.Inst.ActiveAudioPreset?.Config.Name ?? ""; vm.ActiveGamePresetName = GlobalData.Inst.ActiveFunctionPreset?.Name ?? ""; syncing = false; }
    private void RefreshStatus() { if (!ready) return; var g = GlobalData.Inst; vm.Requirements = string.Join("\n", g.GetIncompleteStartupRequirements().Select(x => x.Message)); vm.NowPlaying = g.Playback.Current?.Config.FileName ?? vm.L.GetString("没有匹配的音效"); vm.Progress = g.Playback.Current?.PlaybackProgress ?? 0; vm.PlaybackStatus = vm.L.GetString(g.Playback.IsPlaying ? "播放" : "已停止"); vm.OutputDevice = g.Equipment.PhysicalLoudspeaker?.FriendlyName ?? vm.L.GetString("未绑定"); if (Steamworks.SteamClient.IsValid) Steamworks.SteamClient.RunCallbacks(); }
    private void Navigate(object s, SelectionChangedEventArgs e) { if (LibraryPage == null) return; var pages = new Control[] { LibraryPage, PresetsPage, WorkshopPage, SettingsPage }; for (int i = 0; i < 4; i++) pages[i].IsVisible = i == Navigation.SelectedIndex; vm.PageTitle = vm.L.GetString(new[] { "音效库", "游戏预设", "创意工坊", "设置" }[Math.Clamp(Navigation.SelectedIndex, 0, 3)]); }
    private async void AudioPresetChanged(object s, SelectionChangedEventArgs e)
    {
        if (!ready || syncing || AudioPresets.SelectedItem is not string name) return;
        var request = ++presetRequest;
        await Execute(async () =>
        {
            var preset = await AudioPresetDataTool.LoadAudioPresetData(name);
            if (request != presetRequest || DesktopApplication.Current.Dispatcher.HasShutdownStarted) { preset?.Dispose(); return; }
            GlobalData.Inst.ActiveAudioPreset = preset; selected = null; vm.Filter();
        });
    }
    private void GamePresetChanged(object s, SelectionChangedEventArgs e) { if (!syncing && GamePresets.SelectedItem is string n) Act(() => { GlobalData.Inst.ActiveFunctionPreset = FunctionPresetDataTool.Load(n); vm.Filter(); }); }
    private async void SoundSelected(object s, SelectionChangedEventArgs e) { if (Sounds.SelectedItem is not TrackRow row) return; selected = row.Audio; vm.SelectedName = row.Name; TagsInput.Text = string.Join(", ", selected.Config.Tags); TrackVolume.Value = GlobalData.Inst.GetAudioDecibel(selected); var edit = selected.Config.Edit ?? new(); TrimStart.Value = (decimal)edit.StartSeconds; TrimEnd.Value = (decimal)(edit.EndSeconds ?? selected.AudioTrackArray[0].TotalTime.TotalSeconds); FadeIn.Value = (decimal)edit.FadeInSeconds; FadeOut.Value = (decimal)edit.FadeOutSeconds; waveformCancellation.Cancel(); waveformCancellation.Dispose(); waveformCancellation = new(); var token = waveformCancellation.Token; await Execute(async () => { var peaks = await Task.Run(() => WaveformPeaks.Read(Path.Combine(AudioPresetDataTool.BasePath, row.Audio.AudioPreset.Config.Name, row.Audio.Config.Name), 1024, token), token); if (!token.IsCancellationRequested) { Waveform.Peaks = peaks; Waveform.InvalidateVisual(); } }); }
    private AudioEditSettings Edit() => new() { StartSeconds = (double)(TrimStart.Value ?? 0), EndSeconds = (double)(TrimEnd.Value ?? 0), FadeInSeconds = (double)(FadeIn.Value ?? 0), FadeOutSeconds = (double)(FadeOut.Value ?? 0) };
    private void WaveformChanged(object s, EventArgs e) { if (selected == null) return; var t = selected.AudioTrackArray[0].TotalTime.TotalSeconds; TrimStart.Value = (decimal)(Waveform.Start * t); TrimEnd.Value = (decimal)(Waveform.End * t); }
    private void SaveTrack(object s, RoutedEventArgs e) => Act(() => { if (selected == null) return; var edit = Edit(); edit.Validate(selected.AudioTrackArray[0].TotalTime.TotalSeconds); selected.Config.Edit = edit; selected.Config.Tags = (TagsInput.Text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); GlobalData.Inst.SetAudioDecibel(selected, TrackVolume.Value); if (!selected.AudioPreset.Save()) throw new IOException(vm.L.GetString("保存失败")); vm.Filter(); });
    private void PlayRow(object s, RoutedEventArgs e) => Act(() => { if (((Button)s).Tag is TrackRow row) GlobalData.Inst.Playback.Toggle(row.Audio); });
    private void Preview(object s, RoutedEventArgs e) => Act(() => { if (selected != null) GlobalData.Inst.Playback.Preview(selected, Edit()); });
    private void ToggleFavorite(object s, RoutedEventArgs e) => Act(() => { foreach (var row in Sounds.SelectedItems?.OfType<TrackRow>() ?? []) { row.Audio.Config.IsFavorite = !row.Audio.Config.IsFavorite; row.Audio.AudioPreset.Save(); } vm.Filter(); });
    private void RecordHotkey(object s, RoutedEventArgs e) => Act(() => { if (selected == null) return; var audio = selected; HotkeyManager.Inst.StartRecording(key => { HotkeyManager.Inst.StopRecording(); if (key == null) return; if (HotkeyManager.Inst.IsAssigned(key, GlobalData.Inst.GetAudioHotkey(audio, false))) { vm.Notice = vm.L.GetString("热键冲突"); vm.HasNotice = true; return; } var binding = GlobalData.Inst.GetAudioHotkey(audio, true); binding.VkCode = key.VkCode; binding.Modifiers = key.Modifiers; binding.IsMouse = key.IsMouse; GlobalData.Inst.SaveActiveFunctionPreset(); GlobalData.Inst.RebuildActiveHotkeys(); vm.Filter(); }); });
    private void RecordStopHotkey(object s, RoutedEventArgs e) => Act(() => HotkeyManager.Inst.StartRecording(key => { HotkeyManager.Inst.StopRecording(); if (key == null) return; GlobalData.Inst.ActiveFunctionPreset.StopAllHotkey = key; GlobalData.Inst.SaveActiveFunctionPreset(); GlobalData.Inst.RebuildActiveHotkeys(); vm.StopHotkey = key.ToString(); }));
    private async void ImportAudio(object s, RoutedEventArgs e) { if (!ready) return; await Execute(async () => { if (GlobalData.Inst.ActiveAudioPreset == null) throw new InvalidOperationException(vm.L.GetString("创建音频预设")); var files = await StorageProvider.OpenFilePickerAsync(new() { AllowMultiple = true, FileTypeFilter = [new("Audio") { Patterns = ["*.wav", "*.mp3", "*.ogg", "*.flac", "*.m4a"] }] }); foreach (var f in files) { var path = f.TryGetLocalPath(); if (path != null) await GlobalData.Inst.ActiveAudioPreset.AddAudio(path); } GlobalData.Inst.ActiveAudioPreset.Save(); vm.Filter(); }); }
    private async void CreateAudioPreset(object s, RoutedEventArgs e)
    {
        if (!ready) return;
        await Execute(async () =>
        {
            var input = new TextBox { PlaceholderText = vm.L.GetString("输入音频预设名称"), MinWidth = 300 };
            var dialog = new Window { Title = vm.L.GetString("创建新音频预设"), Width = 380, Height = 180, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var confirm = new Button { Content = vm.L.GetString("确认"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            confirm.Click += (_, _) => dialog.Close(input.Text);
            dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 18, Children = { input, confirm } };
            var name = await dialog.ShowDialog<string>(this);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!AudioPresetDataTool.CreateAudioPresetData(name, out var preset)) throw new IOException(vm.L.GetString("保存失败"));
            GlobalData.Inst.ActiveAudioPreset = preset; RefreshNames(); vm.Filter();
        });
    }
    private void CreateGamePreset(object s, RoutedEventArgs e) => Act(() => { var preset = FunctionPresetDataTool.Create(PresetName.Text); if (preset != null) { GlobalData.Inst.ActiveFunctionPreset = preset; RefreshNames(); } });
    private void RefreshLibrary(object s, RoutedEventArgs e) => Act(() => { RefreshNames(); vm.Filter(); });
    private void DismissNotice(object s, RoutedEventArgs e) => vm.HasNotice = false;
    private void Previous(object s, RoutedEventArgs e) => Act(() => GlobalData.Inst.Playback.Previous());
    private void Next(object s, RoutedEventArgs e) => Act(() => GlobalData.Inst.Playback.Next());
    private void PlayPause(object s, RoutedEventArgs e) => Act(() => GlobalData.Inst.Playback.PauseOrResume());
    private void StopAll(object s, RoutedEventArgs e) => Act(() => GlobalData.Inst.Playback.StopAll());
    private void MasterChanged(object s, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) => Act(() => GlobalData.Inst.AudioProxy.GlobalDecibel = MasterVolume.Value);
    private void RefreshSettings() { syncing = true; ThemeChoices.ItemsSource = ThemeManager.ThemeList.Select(x => x.Name).ToArray(); ThemeChoices.SelectedIndex = (int)GlobalData.Inst.Config.Theme; PaletteChoices.ItemsSource = ThemeManager.Palettes.Keys.ToArray(); PaletteChoices.SelectedItem = GlobalData.Inst.Config.ColorPalette; LanguageChoices.ItemsSource = LanguageManager.Inst.LanguageList.Select(x => x.Value).ToArray(); LanguageChoices.SelectedIndex = LanguageManager.Inst.NowLanguageInfo.Index; Materials.ItemsSource = new[] { "Solid", "Acrylic", "Mica" }; Materials.SelectedItem = GlobalData.Inst.Config.Appearance.WindowMaterial; FontInput.Text = GlobalData.Inst.Config.Appearance.FontFamily; FontSizeInput.Value = (decimal)GlobalData.Inst.Config.Appearance.FontSize; PlaybackModes.ItemsSource = Enum.GetNames<PlaybackMode>(); PlaybackModes.SelectedIndex = (int)GlobalData.Inst.Playback.Mode; syncing = false; RefreshDevices(this, new()); }
    private record DeviceChoice(string Id, string Name) { public override string ToString() => Name; }
    private void RefreshDevices(object s, RoutedEventArgs e) { if (!ready) return; syncing = true; var g = GlobalData.Inst; var combos = new[] { PhysicalMic, PhysicalOutput, VirtualMic, VirtualOutput }; var ids = new[] { g.Config.PhysicalMicrophoneID, g.Config.PhysicalLoudspeakerID, g.Config.VirtualMicrophoneID, g.Config.VirtualLoudspeakerID }; for (int i = 0; i < 4; i++) { var dic = i % 2 == 0 ? g.Equipment.MicrophoneDic : g.Equipment.LoudspeakerDic; var list = dic.Select(x => new DeviceChoice(x.Key, x.Value)).ToArray(); combos[i].ItemsSource = list; combos[i].SelectedItem = list.FirstOrDefault(x => x.Id == ids[i]); } syncing = false; }
    private void DeviceChanged(object s, SelectionChangedEventArgs e) { if (syncing || s is not ComboBox combo || combo.SelectedItem is not DeviceChoice d) return; Act(() => { var equipment = GlobalData.Inst.Equipment; if (combo == PhysicalMic) equipment.PhysicalMicrophone = equipment.GetMicrophone(d.Id); else if (combo == VirtualMic) equipment.VirtualMicrophone = equipment.GetMicrophone(d.Id); else if (combo == PhysicalOutput) equipment.PhysicalLoudspeaker = equipment.GetLoudspeaker(d.Id); else equipment.VirtualLoudspeaker = equipment.GetLoudspeaker(d.Id); }); }
    private void TestChain(object s, RoutedEventArgs e) => Act(() => GlobalData.Inst.TryRun(true));
    private void MonitorChanged(object s, RoutedEventArgs e) => Act(() => { GlobalData.Inst.TryGoEar_Audio(AudioMonitor.IsChecked == true); GlobalData.Inst.TryGoEar_In(MicMonitor.IsChecked == true); GlobalData.Inst.AutoMute = AutoMute.IsChecked == true; });
    private void ThemeSelected(object s, SelectionChangedEventArgs e) { if (!syncing && ThemeChoices.SelectedIndex >= 0) Act(() => ThemeManager.SelectTheme((ThemeManager.ThemeEnum)ThemeChoices.SelectedIndex)); }
    private void PaletteSelected(object s, SelectionChangedEventArgs e) { if (!syncing && PaletteChoices.SelectedItem is string name) Act(() => { var p = ThemeManager.CaptureAppearance(); p.Palette = name; p.Accent = null; ThemeManager.ApplyAppearance(p); }); }
    private void LanguageSelected(object s, SelectionChangedEventArgs e) { if (!syncing && LanguageChoices.SelectedIndex >= 0) Act(() => { LanguageManager.Inst.SetCulture(LanguageManager.Inst.LanguageList[LanguageChoices.SelectedIndex].Key); vm.RefreshLanguage(); }); }
    private void ApplyAppearance(object s, RoutedEventArgs e) => Act(() => { var p = ThemeManager.CaptureAppearance(); p.FontFamily = FontInput.Text; p.FontSize = (double)(FontSizeInput.Value ?? 13); if (!string.IsNullOrWhiteSpace(AccentInput.Text)) p.Accent = AccentInput.Text; ThemeManager.ApplyAppearance(p); });
    private void MaterialSelected(object s, SelectionChangedEventArgs e) { if (!syncing && Materials.SelectedItem is string mode) Act(() => { var p = ThemeManager.CaptureAppearance(); p.WindowMaterial = mode; ThemeManager.ApplyAppearance(p); TransparencyLevelHint = mode switch { "Mica" => [WindowTransparencyLevel.Mica], "Acrylic" => [WindowTransparencyLevel.AcrylicBlur], _ => [WindowTransparencyLevel.None] }; }); }
    private void PlaybackModeChanged(object s, SelectionChangedEventArgs e) { if (!syncing && PlaybackModes.SelectedIndex >= 0) Act(() => GlobalData.Inst.Playback.SetMode((PlaybackMode)PlaybackModes.SelectedIndex)); }
    private async void ImportTheme(object s, RoutedEventArgs e) { await Execute(async () => { var files = await StorageProvider.OpenFilePickerAsync(new() { AllowMultiple = false }); if (files.FirstOrDefault()?.TryGetLocalPath() is string path) { if (new FileInfo(path).Length > 65536) throw new InvalidDataException(); ThemeManager.ApplyAppearance(AppearanceProfile.Parse(await File.ReadAllTextAsync(path))); RefreshSettings(); } }); }
    private async void ExportTheme(object s, RoutedEventArgs e) { await Execute(async () => { var file = await StorageProvider.SaveFilePickerAsync(new() { SuggestedFileName = "PlayVoice-theme.json" }); if (file?.TryGetLocalPath() is string path && !JsonTool.SaveJsonAtomically(path, ThemeManager.CaptureAppearance())) throw new IOException(); }); }
    private async void SearchWorkshop(object s, RoutedEventArgs e)
    {
        if (!ready) return;
        await Execute(async () =>
        {
            if (!Steamworks.SteamClient.IsValid) Steamworks.SteamClient.Init(4907460);
            var query = Steamworks.Ugc.Query.All.AllowCachedResponse(10);
            if (!string.IsNullOrWhiteSpace(WorkshopSearch.Text)) query = query.WhereSearchText(WorkshopSearch.Text).RankedByTextSearch();
            var page = await query.GetPageAsync(1);
            if (page == null) throw new IOException(vm.L.GetString("加载失败"));
            using var result = page.Value;
            var cards = new List<Control>();
            foreach (var item in result.Entries) { var entry = item; var button = new Button { Content = entry.Title, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Margin = new Avalonia.Thickness(0, 0, 0, 10) }; button.Click += async (_, _) => await Execute(async () => { if (!await entry.Subscribe()) throw new IOException(vm.L.GetString("加载失败")); vm.Notice = entry.Title; vm.HasNotice = true; }); cards.Add(button); }
            WorkshopItems.ItemsSource = cards; vm.SteamStatus = cards.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        });
    }
}
