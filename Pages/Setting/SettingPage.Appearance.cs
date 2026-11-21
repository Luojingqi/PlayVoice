using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PlayVoice.Resources.Themes;
using PlayVoice.Resources.Language;

namespace PlayVoice.Pages.Setting;

public partial class SettingPage
{
    private bool refreshingAppearance;
    private void InitializeAppearanceEditor()
    {
        FontChoices.ItemsSource = Fonts.SystemFontFamilies.Select(font => font.Source).OrderBy(name => name).ToArray();
        CustomThemeChoices.SelectionChanged += (_, _) =>
        {
            if (!refreshingAppearance && CustomThemeChoices.SelectedItem is string name
                && GlobalData.Inst.Config.CustomThemes.TryGetValue(name, out var profile))
                AppearanceAction(() => ThemeManager.ApplyAppearance(profile));
        };
    }

    private void RefreshAppearanceEditor()
    {
        refreshingAppearance = true;
        var config = GlobalData.Inst.Config;
        config.CustomThemes ??= new();
        var profile = ThemeManager.CaptureAppearance();
        ThemeNameInput.Text = profile.Name;
        FontChoices.SelectedItem = profile.FontFamily;
        if (FontChoices.SelectedItem == null) FontChoices.SelectedItem = "Segoe UI";
        FontSizeInput.Value = profile.FontSize;
        string CurrentColor(string key)
        {
            var color = ((SolidColorBrush)Application.Current.FindResource(key)).Color;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        AccentInput.Text = string.IsNullOrEmpty(profile.Accent) ? CurrentColor("AccentColor") : profile.Accent;
        BackgroundInput.Text = string.IsNullOrEmpty(profile.Background) ? CurrentColor("BgDark") : profile.Background;
        ForegroundInput.Text = string.IsNullOrEmpty(profile.Foreground) ? CurrentColor("TextPrimary") : profile.Foreground;
        CustomThemeChoices.ItemsSource = config.CustomThemes.Keys.OrderBy(name => name).ToArray();
        CustomThemeChoices.SelectedItem = config.CustomThemes.ContainsKey(profile.Name) ? profile.Name : null;
        refreshingAppearance = false;
    }

    private AppearanceProfile ReadAppearanceEditor()
    {
        var profile = ThemeManager.CaptureAppearance();
        profile.Name = ThemeNameInput.Text.Trim();
        profile.FontFamily = FontChoices.SelectedItem as string ?? "Segoe UI";
        profile.FontSize = FontSizeInput.Value;
        profile.Accent = AccentInput.Text.Trim();
        profile.Background = BackgroundInput.Text.Trim();
        profile.Foreground = ForegroundInput.Text.Trim();
        profile.Validate();
        return profile;
    }

    private void AppearanceAction(Action action)
    {
        try { action(); AppearanceError.Text = ""; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or ArgumentException)
        {
            Diagnostics.AppLog.Warning(exception, "外观设置未应用。", "Appearance");
            AppearanceError.Text = LanguageManager.Inst.GetString("主题文件无效");
        }
    }

    private void ApplyAppearanceClick(object sender, RoutedEventArgs e) => AppearanceAction(() => ThemeManager.ApplyAppearance(ReadAppearanceEditor()));
    private void SaveAppearanceClick(object sender, RoutedEventArgs e) => AppearanceAction(() =>
    {
        var profile = ReadAppearanceEditor();
        GlobalData.Inst.Config.CustomThemes[profile.Name] = profile.Copy();
        ThemeManager.ApplyAppearance(profile);
        RefreshChoices();
    });
    private void ResetAppearanceClick(object sender, RoutedEventArgs e) => AppearanceAction(() => ThemeManager.ApplyAppearance(new AppearanceProfile()));
    private void ImportAppearanceClick(object sender, RoutedEventArgs e) => AppearanceAction(() =>
    {
        var dialog = new OpenFileDialog { Filter = "JSON|*.json", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        if (new FileInfo(dialog.FileName).Length > 65536) throw new InvalidDataException();
        var profile = AppearanceProfile.Parse(File.ReadAllText(dialog.FileName));
        GlobalData.Inst.Config.CustomThemes[profile.Name] = profile.Copy();
        ThemeManager.ApplyAppearance(profile);
        RefreshChoices();
    });
    private void ExportAppearanceClick(object sender, RoutedEventArgs e) => AppearanceAction(() =>
    {
        var profile = ReadAppearanceEditor();
        var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "PlayVoice-theme.json", DefaultExt = ".json" };
        if (dialog.ShowDialog() != true) return;
        if (!JsonTool.SaveJsonAtomically(dialog.FileName, profile)) throw new IOException();
    });
    private void ChooseAppearanceColor(object sender, RoutedEventArgs e)
    {
        var input = (((Button)sender).Tag as string) switch { "Accent" => AccentInput, "Background" => BackgroundInput, _ => ForegroundInput };
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            input.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }
}
