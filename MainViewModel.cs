using CommunityToolkit.Mvvm.ComponentModel;
using PlayVoice.Pages.Sidebar;
using PlayVoice.Resources.Language;
using System.Collections.ObjectModel;

namespace PlayVoice;

internal class MainViewModel : ObservableObject
{
    public ObservableCollection<SidebarItemViewModel> MenuItems { get; } = new();

    private SidebarItemViewModel _selectedMenu;
    public SidebarItemViewModel SelectedMenu
    {
        get => _selectedMenu;
        set => SetProperty(ref _selectedMenu, value);
    }

    public MainViewModel()
    {
        InitializeMenuItems();
        SelectedMenu = MenuItems[0];
        
    }

    private void InitializeMenuItems()
    {
        MenuItems.Add(new SidebarItemViewModel("预设")
        {
            PageUri = "Pages/Preset/PresetPage.xaml"
        });

        MenuItems.Add(new SidebarItemViewModel("创意工坊")
        {
            PageUri = "Pages/Workshop/WorkshopPage.xaml"
        });
        MenuItems.Add(new SidebarItemViewModel("设置")
        {
            PageUri = "Pages/Setting/SettingPage.xaml"
        });
    }

    
}
