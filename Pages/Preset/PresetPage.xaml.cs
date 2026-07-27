using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlayVoice.Pages.Preset;

/// <summary>
/// PresetPage.xaml 的交互逻辑
/// </summary>
public partial class PresetPage : Page
{
    public static PresetPage Inst { get; private set; }
    public int Count => PageList.Count;
    private List<PageData> PageList = new();
    private AudioTrackGrid AudioTrackGridPage;

    private CreatePresetPage CreatePresetPage;
    private DeletePresetPage DeletePresetPage;
    private UploadPresetPage UploadPresetPage;
    private bool isSynchronizingPresetSelection;
    private bool isLoadingPresetFromPage;

    public PresetPage()
    {
        Inst = this;
        InitializeComponent();
        AudioTrackGridPage = new AudioTrackGrid();
        Frame0.Content = AudioTrackGridPage;
        Frame0.Visibility = Visibility.Hidden;
        Frame1.Visibility = Visibility.Hidden;
        CreatePresetPage = new();
        DeletePresetPage = new();
        UploadPresetPage = new();
        CreatePresetPageFrame.Content = CreatePresetPage;
        DeletePresetPageFrame.Content = DeletePresetPage;
        UploadPresetPageFrame.Content = UploadPresetPage;
        Loaded += PresetPage_Loaded;
        Unloaded += PresetPage_Unloaded;
        var presetNames = PresetDataTool.GetAllPresetName();
        for (int i = 0; i < presetNames.Length; i++)
        {
            var name = presetNames[i];
            PageList.Add(new PageData() { Name = name });
        }
        PageList.Add(new PageData() { Name = " + " });
        TopButtonListBox.ItemsSource = PageList;
        TopButtonListBox.DisplayMemberPath = "Name";
        TopButtonListBox.SelectionChanged += TopButtonListBox_SelectionChanged;

    }

    private async void TopButtonListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isSynchronizingPresetSelection) return;

        var selectedPage = (PageData)TopButtonListBox.SelectedItem;
        if (string.IsNullOrEmpty(selectedPage?.Name))
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = Visibility.Hidden;
        }
        else if (selectedPage.Name == " + ")
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = Visibility.Visible;
            DeletePresetPage.Open();
            UploadPresetPage.Open(-1);
        }
        else
        {
            Frame0.Visibility = Visibility.Visible;
            Frame1.Visibility = Visibility.Hidden;
            isLoadingPresetFromPage = true;
            try
            {
                await AudioTrackGridPage.InitLoadPreset(selectedPage.Name);
            }
            finally
            {
                isLoadingPresetFromPage = false;
            }
        }
    }

    private void PresetPage_Loaded(object sender, RoutedEventArgs e)
    {
        GlobalData.Inst.PresetDataChanged -= UpdatePresetPage;
        GlobalData.Inst.PresetDataChanged += UpdatePresetPage;
        UpdatePresetPage(GlobalData.Inst.PresetData);
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs e)
    {
        GlobalData.Inst.PresetDataChanged -= UpdatePresetPage;
    }

    private async void UpdatePresetPage(PresetData presetData)
    {
        if (isLoadingPresetFromPage) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdatePresetPage(presetData));
            return;
        }

        int selectedIndex = PageList.Count - 1;
        if (presetData != null)
        {
            int presetIndex = PageList.FindIndex(page => page.Name == presetData.Config.Name);
            if (presetIndex >= 0)
                selectedIndex = presetIndex;
        }

        isSynchronizingPresetSelection = true;
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
            isSynchronizingPresetSelection = false;
        }

        if (presetData == null)
        {
            Frame0.Visibility = Visibility.Hidden;
            Frame1.Visibility = Visibility.Visible;
            DeletePresetPage.Open();
            UploadPresetPage.Open(-1);
        }
        else
        {
            Frame0.Visibility = Visibility.Visible;
            Frame1.Visibility = Visibility.Hidden;
            await AudioTrackGridPage.RefreshCurrentPreset();
        }
    }

    private void DisableNavigation_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // 禁止执行该命令
        e.CanExecute = false;
        // 标记为已处理，防止路由事件继续传递
        e.Handled = true;
    }
    public void AddPresetPage(PresetData presetData)
    {
        var newPage = new PageData { Name = presetData.Config.Name };
        PageList.Insert(PageList.Count - 1, newPage);
        TopButtonListBox.Items.Refresh();
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TopButtonListBox.SelectedIndex = PageList.Count - 2;
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    }

    public void RemovePresetPage(string presetName)
    {
        var pageToRemove = PageList.FirstOrDefault(p => p.Name == presetName);
        if (pageToRemove != null)
        {
            PageList.Remove(pageToRemove);
            TopButtonListBox.Items.Refresh();
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TopButtonListBox.SelectedIndex = PageList.Count - 1;
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    public class PageData
    {
        public string Name { get; set; }
    }
}
