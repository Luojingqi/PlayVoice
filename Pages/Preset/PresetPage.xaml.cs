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
    private readonly CreatePresetPage createAudioPresetPage;
    private readonly CreateFunctionPresetPage createFunctionPresetPage;
    private readonly DeletePresetPage deleteAudioPresetPage;
    private readonly DeleteFunctionPresetPage deleteFunctionPresetPage;
    private readonly UploadPresetPage uploadPresetPage;
    private List<FunctionPresetData> functionPresets = new();
    private bool isSynchronizingAudioPresetSelection;
    private bool isSynchronizingFunctionPresetSelection;
    private bool isLoadingAudioPresetFromPage;

    public PresetPage()
    {
        Inst = this;
        InitializeComponent();

        audioTrackGridPage = new AudioTrackGrid();
        Frame0.Content = audioTrackGridPage;
        Frame0.Visibility = Visibility.Hidden;
        Frame1.Visibility = Visibility.Hidden;
        Frame2.Visibility = Visibility.Hidden;

        createAudioPresetPage = new CreatePresetPage();
        createFunctionPresetPage = new CreateFunctionPresetPage();
        deleteAudioPresetPage = new DeletePresetPage();
        deleteFunctionPresetPage = new DeleteFunctionPresetPage();
        uploadPresetPage = new UploadPresetPage();
        CreateAudioPresetPageFrame.Content = createAudioPresetPage;
        CreateFunctionPresetPageFrame.Content = createFunctionPresetPage;
        DeleteAudioPresetPageFrame.Content = deleteAudioPresetPage;
        DeleteFunctionPresetPageFrame.Content = deleteFunctionPresetPage;
        UploadPresetPageFrame.Content = uploadPresetPage;

        foreach (var config in AudioPresetDataTool.GetAllAudioPresetConfigs())
            pageList.Add(new PageData { Name = config.Name });
        pageList.Add(PageData.CreateAddPage());
        pageList.Add(PageData.CreateDeletePage());

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

        var selectedPage = TopButtonListBox.SelectedItem as PageData;
        if (selectedPage == null)
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = Visibility.Hidden;
            Frame2.Visibility = Visibility.Hidden;
        }
        else if (selectedPage.IsAddPage)
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = Visibility.Visible;
            Frame2.Visibility = Visibility.Hidden;
            uploadPresetPage.Open(-1);
        }
        else if (selectedPage.IsDeletePage)
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = Visibility.Hidden;
            Frame2.Visibility = Visibility.Visible;
            deleteAudioPresetPage.Open();
            deleteFunctionPresetPage.Open();
        }
        else
        {
            Frame0.Visibility = Visibility.Visible;
            Frame1.Visibility = Visibility.Hidden;
            Frame2.Visibility = Visibility.Hidden;
            isLoadingAudioPresetFromPage = true;
            try
            {
                await audioTrackGridPage.InitLoadAudioPreset(selectedPage.Name);
            }
            finally
            {
                isLoadingAudioPresetFromPage = false;
            }
        }
    }

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

        RefreshFunctionPresetList();
        var audioPresetBeforeRestore = GlobalData.Inst.ActiveAudioPreset;
        await GlobalData.Inst.RestoreActiveAudioPresetAsync();
        if (ReferenceEquals(audioPresetBeforeRestore, GlobalData.Inst.ActiveAudioPreset))
            UpdateAudioPresetPage(GlobalData.Inst.ActiveAudioPreset);
        UpdateFunctionPresetSelection(GlobalData.Inst.ActiveFunctionPreset);
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs e)
    {
        GlobalData.Inst.ActiveAudioPresetChanged -= UpdateAudioPresetPage;
        GlobalData.Inst.ActiveFunctionPresetChanged -= UpdateFunctionPresetSelection;
        LanguageManager.Inst.CultureChanged -= UpdateLanguage;
    }

    private void UpdateLanguage(
        System.Globalization.CultureInfo culture,
        LanguageManager.LanguageInfo languageInfo)
    {
        FunctionPresetComboBox.Items.Refresh();
        deleteFunctionPresetPage.RefreshLanguage();
    }

    public void RefreshFunctionPresetList()
    {
        functionPresets = FunctionPresetDataTool.GetAll();
        if (functionPresets.Count == 0)
        {
            var defaultPreset = FunctionPresetDataTool.CreateDefault();
            if (defaultPreset != null)
                functionPresets.Add(defaultPreset);
        }

        FunctionPresetComboBox.ItemsSource = functionPresets;
        var selectedPreset = functionPresets.FirstOrDefault(item =>
            string.Equals(
                item.Name,
                GlobalData.Inst.ActiveFunctionPreset?.Name,
                StringComparison.OrdinalIgnoreCase));
        if (selectedPreset != null
            && !ReferenceEquals(selectedPreset, GlobalData.Inst.ActiveFunctionPreset))
            GlobalData.Inst.ActiveFunctionPreset = selectedPreset;
    }

    private async void UpdateAudioPresetPage(AudioPresetData audioPreset)
    {
        if (isLoadingAudioPresetFromPage) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateAudioPresetPage(audioPreset));
            return;
        }

        bool keepDeleteManagementPage =
            (TopButtonListBox.SelectedItem as PageData)?.IsDeletePage == true;
        int selectedIndex = pageList.FindIndex(page => keepDeleteManagementPage
            ? page.IsDeletePage
            : page.IsAddPage);
        if (audioPreset != null)
        {
            int presetIndex = pageList.FindIndex(page => string.Equals(
                page.Name,
                audioPreset.Config.Name,
                StringComparison.OrdinalIgnoreCase));
            if (presetIndex >= 0)
                selectedIndex = presetIndex;
        }

        isSynchronizingAudioPresetSelection = true;
        try
        {
            TopButtonListBox.SelectedIndex = -1;
            await Dispatcher.InvokeAsync(() =>
            {
                TopButtonListBox.UpdateLayout();
                TopButtonListBox.SelectedIndex = selectedIndex;
                TopButtonListBox.ScrollIntoView(TopButtonListBox.SelectedItem);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        finally
        {
            isSynchronizingAudioPresetSelection = false;
        }

        if (audioPreset == null)
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = keepDeleteManagementPage
                ? Visibility.Hidden
                : Visibility.Visible;
            Frame2.Visibility = keepDeleteManagementPage
                ? Visibility.Visible
                : Visibility.Hidden;
            if (keepDeleteManagementPage)
            {
                deleteAudioPresetPage.Open();
                deleteFunctionPresetPage.Open();
            }
            else
            {
                uploadPresetPage.Open(-1);
            }
        }
        else
        {
            Frame0.Visibility = Visibility.Visible;
            Frame1.Visibility = Visibility.Hidden;
            Frame2.Visibility = Visibility.Hidden;
            await audioTrackGridPage.RefreshCurrentAudioPreset();
        }
    }

    private void UpdateFunctionPresetSelection(FunctionPresetData functionPreset)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateFunctionPresetSelection(functionPreset));
            return;
        }

        int selectedIndex = functionPresets.FindIndex(item => string.Equals(
            item.Name,
            functionPreset?.Name,
            StringComparison.OrdinalIgnoreCase));
        isSynchronizingFunctionPresetSelection = true;
        FunctionPresetComboBox.SelectedIndex = selectedIndex;
        isSynchronizingFunctionPresetSelection = false;
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

    public void RemoveAudioPresetPage(string name)
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
