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
        CreatePresetPageFrame.Content = CreatePresetPage;
        DeletePresetPageFrame.Content = DeletePresetPage;
        int index = -1;
        var presetNames = PresetDataTool.GetAllPresetName();
        for (int i = 0; i < presetNames.Length; i++)
        {
            var name = presetNames[i];
            PageList.Add(new PageData() { Name = name });
            if (GlobalData.Inst.PresetData != null && name == GlobalData.Inst.PresetData.Config.Name)
                index = i;
        }
        PageList.Add(new PageData() { Name = " + " });
        if (index == -1) index = PageList.Count - 1;
        TopButtonListBox.ItemsSource = PageList;
        TopButtonListBox.DisplayMemberPath = "Name";
        TopButtonListBox.SelectionChanged += TopButtonListBox_SelectionChanged;
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TopButtonListBox.SelectedIndex = index;
            
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    }

    private void TopButtonListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

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
        }
        else
        {
            Frame0.Visibility = Visibility.Visible;
            Frame1.Visibility = Visibility.Hidden;
            AudioTrackGridPage.InitLoadPreset(selectedPage.Name);
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
