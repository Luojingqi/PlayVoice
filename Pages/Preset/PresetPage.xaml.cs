using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Resources.Language;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlayVoice.Pages.Preset;

/// <summary>
/// 音频预设页面的交互逻辑。
/// </summary>
public partial class PresetPage : Page
{
    public static PresetPage Inst { get; private set; }

    private readonly List<PageData> pageList = new();
    private readonly AudioTrackGrid audioTrackGridPage;
    private UploadPresetPage uploadPresetPage;
    private DeletePresetPage deleteAudioPresetPage;
    private DeleteFunctionPresetPage deleteFunctionPresetPage;
    private CancellationTokenSource pageRequest;
    private List<FunctionPresetData> functionPresets = new();
    private bool isSynchronizingAudioPresetSelection;
    private bool isSynchronizingFunctionPresetSelection;

    public PresetPage()
    {
        Inst = this;
        InitializeComponent();

        audioTrackGridPage = new AudioTrackGrid();
        Frame0.Content = audioTrackGridPage;
        Frame0.Visibility = Frame1.Visibility = Frame2.Visibility = Visibility.Collapsed;

        TopButtonListBox.ItemsSource = pageList;
        TopButtonListBox.DisplayMemberPath = nameof(PageData.Name);
        TopButtonListBox.SelectionChanged += TopButtonListBox_SelectionChanged;
        FunctionPresetComboBox.SelectionChanged += FunctionPresetComboBox_SelectionChanged;

        Loaded += PresetPage_Loaded;
        Unloaded += PresetPage_Unloaded;
    }

    private async void TopButtonListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isSynchronizingAudioPresetSelection) return;

        pageRequest?.Cancel();
        if (TopButtonListBox.SelectedItem is not PageData page) return;
        ShowPage(page);
        if (page.IsAddPage || page.IsDeletePage) return;

        using var request = new CancellationTokenSource();
        pageRequest = request;
        try
        {
            Frame0.IsEnabled = false;
            bool selected = await GlobalData.Inst.SelectAudioPresetAsync(page.Name, request.Token);
            if (request.IsCancellationRequested) return;
            if (selected) audioTrackGridPage.RefreshCurrentAudioPreset();
            else UpdateAudioPresetPage(GlobalData.Inst.ActiveAudioPreset);
        }
        catch (Exception exception)
        {
            if (!request.IsCancellationRequested)
            {
                audioTrackGridPage.ShowLibraryError(exception);
                UpdateAudioPresetPage(GlobalData.Inst.ActiveAudioPreset);
            }
        }
        finally
        {
            if (ReferenceEquals(pageRequest, request))
            {
                pageRequest = null;
                Frame0.IsEnabled = true;
            }
        }
    }

    private void ShowPage(PageData page)
    {
        Frame0.Visibility = !page.IsAddPage && !page.IsDeletePage ? Visibility.Visible : Visibility.Collapsed;
        Frame1.Visibility = page.IsAddPage ? Visibility.Visible : Visibility.Collapsed;
        Frame2.Visibility = page.IsDeletePage ? Visibility.Visible : Visibility.Collapsed;
        if (page.IsAddPage)
        {
            if (uploadPresetPage == null)
            {
                CreateAudioPresetPageFrame.Content = new CreatePresetPage();
                CreateFunctionPresetPageFrame.Content = new CreateFunctionPresetPage();
                UploadPresetPageFrame.Content = uploadPresetPage = new UploadPresetPage();
            }
            uploadPresetPage.Open(-1, pageList.Where(IsAudioPage).Select(p => p.Name).ToArray());
        }
        else if (page.IsDeletePage)
        {
            if (deleteAudioPresetPage == null)
            {
                DeleteAudioPresetPageFrame.Content = deleteAudioPresetPage = new DeletePresetPage();
                DeleteFunctionPresetPageFrame.Content = deleteFunctionPresetPage = new DeleteFunctionPresetPage();
            }
            deleteAudioPresetPage.Open();
            deleteFunctionPresetPage.Open();
        }
    }

    private static bool IsAudioPage(PageData page) => !page.IsAddPage && !page.IsDeletePage;

    private void FunctionPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isSynchronizingFunctionPresetSelection) return;
        if (FunctionPresetComboBox.SelectedItem is FunctionPresetData functionPreset)
            GlobalData.Inst.ActiveFunctionPreset = functionPreset;
    }

    private async void PresetPage_Loaded(object sender, RoutedEventArgs e)
    {
        GlobalData.Inst.ActiveAudioPresetChanged -= UpdateAudioPresetPage;
        GlobalData.Inst.ActiveAudioPresetChanged += UpdateAudioPresetPage;
        GlobalData.Inst.ActiveFunctionPresetChanged -= UpdateFunctionPresetSelection;
        GlobalData.Inst.ActiveFunctionPresetChanged += UpdateFunctionPresetSelection;
        LanguageManager.Inst.CultureChanged -= UpdateLanguage;
        LanguageManager.Inst.CultureChanged += UpdateLanguage;

        pageRequest?.Cancel();
        using var request = new CancellationTokenSource();
        pageRequest = request;
        IsEnabled = false;
        try
        {
            var catalog = await Task.Run(() => (
                Names: AudioPresetDataTool.GetAllAudioPresetName(),
                Functions: ReadFunctionPresets()), request.Token);
            if (request.IsCancellationRequested) return;
            if (pageList.Count == 0 || !catalog.Names.SequenceEqual(pageList.Where(IsAudioPage).Select(p => p.Name)))
            {
                isSynchronizingAudioPresetSelection = true;
                try
                {
                    pageList.Clear();
                    pageList.AddRange(catalog.Names.Select(name => new PageData { Name = name }));
                    pageList.Add(PageData.CreateAddPage());
                    pageList.Add(PageData.CreateDeletePage());
                    TopButtonListBox.Items.Refresh();
                }
                finally { isSynchronizingAudioPresetSelection = false; }
            }
            SetFunctionPresets(catalog.Functions);
            IsEnabled = true;
            var global = GlobalData.Inst;
            var before = global.ActiveAudioPreset;
            await global.RestoreActiveAudioPresetAsync(request.Token);
            if (!request.IsCancellationRequested && ReferenceEquals(before, global.ActiveAudioPreset))
                UpdateAudioPresetPage(global.ActiveAudioPreset);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!request.IsCancellationRequested)
            {
                audioTrackGridPage.ShowLibraryError(exception);
                UpdateAudioPresetPage(GlobalData.Inst.ActiveAudioPreset);
            }
        }
        finally
        {
            if (ReferenceEquals(pageRequest, request))
            {
                pageRequest = null;
                IsEnabled = true;
            }
        }
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs e)
    {
        pageRequest?.Cancel();
        GlobalData.Inst.ActiveAudioPresetChanged -= UpdateAudioPresetPage;
        GlobalData.Inst.ActiveFunctionPresetChanged -= UpdateFunctionPresetSelection;
        LanguageManager.Inst.CultureChanged -= UpdateLanguage;
    }

    private void UpdateLanguage(
        System.Globalization.CultureInfo culture,
        LanguageManager.LanguageInfo languageInfo)
    {
        FunctionPresetComboBox.Items.Refresh();
        deleteFunctionPresetPage?.RefreshLanguage();
    }

    private static List<FunctionPresetData> ReadFunctionPresets()
    {
        var presets = FunctionPresetDataTool.GetAll();
        if (presets.Count == 0 && FunctionPresetDataTool.CreateDefault() is { } defaultPreset)
            presets.Add(defaultPreset);
        return presets;
    }

    public void RefreshFunctionPresetList() => SetFunctionPresets(ReadFunctionPresets());

    private void SetFunctionPresets(List<FunctionPresetData> presets)
    {
        functionPresets = presets;
        isSynchronizingFunctionPresetSelection = true;
        try
        {
            var active = GlobalData.Inst.ActiveFunctionPreset;
            int index = FindFunctionPreset(active);
            if (index >= 0) presets[index] = active;
            FunctionPresetComboBox.ItemsSource = presets;
            FunctionPresetComboBox.SelectedIndex = index;
        }
        finally { isSynchronizingFunctionPresetSelection = false; }
        audioTrackGridPage.RefreshFunctionPresetValues();
    }

    private int FindFunctionPreset(FunctionPresetData preset) => functionPresets.FindIndex(item =>
        string.Equals(item.Name, preset?.Name, StringComparison.OrdinalIgnoreCase));

    private void UpdateAudioPresetPage(AudioPresetData audioPreset)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateAudioPresetPage(audioPreset));
            return;
        }

        var active = pageList.FirstOrDefault(page => IsAudioPage(page)
            && string.Equals(page.Name, audioPreset?.Config.Name, StringComparison.OrdinalIgnoreCase));
        var selected = active ?? (TopButtonListBox.SelectedItem is PageData { IsDeletePage: true } delete
            ? delete : pageList.FirstOrDefault(page => page.IsAddPage));
        if (selected == null) return;
        isSynchronizingAudioPresetSelection = true;
        try
        {
            TopButtonListBox.SelectedItem = selected;
            TopButtonListBox.ScrollIntoView(selected);
        }
        finally { isSynchronizingAudioPresetSelection = false; }
        Frame0.IsEnabled = true;
        ShowPage(selected);
        audioTrackGridPage.RefreshCurrentAudioPreset();
    }

    private void UpdateFunctionPresetSelection(FunctionPresetData functionPreset)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateFunctionPresetSelection(functionPreset));
            return;
        }

        int selectedIndex = FindFunctionPreset(functionPreset);
        isSynchronizingFunctionPresetSelection = true;
        try
        {
            if (selectedIndex >= 0 && !ReferenceEquals(functionPresets[selectedIndex], functionPreset))
            {
                functionPresets[selectedIndex] = functionPreset;
                FunctionPresetComboBox.Items.Refresh();
            }
            FunctionPresetComboBox.SelectedIndex = selectedIndex;
        }
        finally { isSynchronizingFunctionPresetSelection = false; }
        audioTrackGridPage.RefreshFunctionPresetValues();
    }

    private void DisableNavigation_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = false;
        e.Handled = true;
    }

    public void AddAudioPresetPage(AudioPresetData audioPreset)
    {
        var newPage = new PageData
        {
            Name = audioPreset.Config.Name
        };
        int managementPageIndex = pageList.FindIndex(page => page.IsAddPage || page.IsDeletePage);
        pageList.Insert(managementPageIndex, newPage);
        TopButtonListBox.Items.Refresh();
        GlobalData.Inst.ActiveAudioPreset = audioPreset;
    }

    public void AddFunctionPreset(FunctionPresetData functionPreset)
    {
        GlobalData.Inst.ActiveFunctionPreset = functionPreset;
        RefreshFunctionPresetList();
    }

    public void SelectCreateManagementPage()
    {
        TopButtonListBox.SelectedIndex = pageList.FindIndex(page => page.IsAddPage);
    }
    public void RemoveAudioPresetPage(string name, bool keepManagementPage = false)
    {
        var pageToRemove = pageList.FirstOrDefault(page =>
            string.Equals(page.Name, name, StringComparison.OrdinalIgnoreCase));
        if (pageToRemove == null) return;

        pageList.Remove(pageToRemove);
        TopButtonListBox.Items.Refresh();
        if (GlobalData.Inst.ActiveAudioPreset == null)
            SelectCreateManagementPage();
    }

    public class PageData
    {
        public string Name { get; set; }
        public bool IsAddPage { get; set; }
        public bool IsDeletePage { get; set; }

        public static PageData CreateAddPage() => new()
        {
            Name = " + ",
            IsAddPage = true
        };

        public static PageData CreateDeletePage() => new()
        {
            Name = " - ",
            IsDeletePage = true
        };
    }
}
