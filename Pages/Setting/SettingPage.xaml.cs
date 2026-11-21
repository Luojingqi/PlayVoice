using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
namespace PlayVoice.Pages.Setting;
public partial class SettingPage : Page
{
    public SettingPage()
    {
        InitializeComponent();
        InitializeAppearanceEditor();
        Loaded += OnLoaded;
        Unloaded += (_, _) => { ThemeManager.ThemeChanged -= ThemeChanged; LanguageManager.Inst.CultureChanged -= LanguageChanged; };
        AppearanceChoices.SelectionChanged += (_, _) => { if (AppearanceChoices.SelectedItem is ThemeManager.ThemeInfo theme && theme != ThemeManager.NowTheme) ThemeManager.SwitchTheme(theme.Theme); };
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged += ThemeChanged;
        LanguageManager.Inst.CultureChanged += LanguageChanged;
        RefreshChoices();
    }
    private void ThemeChanged(ThemeManager.ThemeInfo theme) => RefreshChoices();
    private void LanguageChanged(System.Globalization.CultureInfo culture, LanguageManager.LanguageInfo language) => RefreshChoices();
    private void RefreshChoices()
    {
        AppearanceChoices.ItemsSource = null;
        AppearanceChoices.ItemsSource = ThemeManager.ThemeList;
        AppearanceChoices.SelectedItem = ThemeManager.NowTheme;
        RefreshAppearanceEditor();
        PaletteChoices.Children.Clear();
        foreach (var palette in ThemeManager.Palettes)
        {
            var content = new StackPanel();
            content.Children.Add(new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Value.Light[0])), Width = 100, Height = 54, CornerRadius = new CornerRadius(8), Margin = new Thickness(0,0,0,12) });
            content.Children.Add(new TextBlock { Text = LanguageManager.Inst.GetString(palette.Value.Name) });
            var button = new Button { Content = content, Margin = new Thickness(0,0,12,12), Padding = new Thickness(16), BorderThickness = new Thickness(1) };
            button.SetResourceReference(Control.BorderBrushProperty, GlobalData.Inst.Config.ColorPalette == palette.Key ? "AccentColor" : "Border");
            button.Click += (_, _) => { ThemeManager.SwitchPalette(palette.Key); RefreshChoices(); };
            PaletteChoices.Children.Add(button);
        }
    }
}
