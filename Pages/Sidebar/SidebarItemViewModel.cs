using CommunityToolkit.Mvvm.ComponentModel;
using PlayVoice.Resources.Language;

namespace PlayVoice.Pages.Sidebar;

public partial class SidebarItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _title = string.Empty;
    private string _titleKey = string.Empty;

    public SidebarItemViewModel(string titleKey)
    {
        _titleKey = titleKey;
        RefreshTitle();
        LanguageManager.Inst.CultureChanged += UpdateLanguageAction;
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string PageUri { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private void RefreshTitle()
    {
        Title = LanguageManager.Inst.GetString(_titleKey);
    }

    public void UpdateLanguageAction(System.Globalization.CultureInfo culture, LanguageManager.LanguageInfo languageInfo)
    {
        RefreshTitle();
    }
}
