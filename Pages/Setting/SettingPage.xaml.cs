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
        AppearanceChoices.SelectionChanged += (_, _) => { if (AppearanceChoices.SelectedItem is ThemeManager.ThemeInfo theme && theme != ThemeManager.NowTheme) ThemeManager.SelectTheme(theme.Theme); };
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
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette.Value.Light[0])), Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = LanguageManager.Inst.GetString(palette.Value.Name), VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Content = content, Margin = new Thickness(0,3,6,3), Padding = new Thickness(10,6,10,6), BorderThickness = new Thickness(1) };
            button.SetResourceReference(Control.BackgroundProperty, GlobalData.Inst.Config.ColorPalette == palette.Key ? "AccentSurface" : "BgLight");
            button.SetResourceReference(Control.BorderBrushProperty, GlobalData.Inst.Config.ColorPalette == palette.Key ? "AccentColor" : "Border");
            button.Click += (_, _) => ThemeManager.SwitchPalette(palette.Key);
            PaletteChoices.Children.Add(button);
        }
    }
}
