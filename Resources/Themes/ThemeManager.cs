using Microsoft.Win32;
using PlayVoice.Resources.Language;
using PlayVoice.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Text.Json;
namespace PlayVoice.Resources.Themes;

public static class ThemeManager
{
    public static ThemeInfo NowTheme => nowNowTheme;
    private static ThemeInfo nowNowTheme;
    public enum ThemeEnum
    {
        Light,
        Dark,
        System
    }

    public sealed class PaletteDefinition
    {
        public string Name { get; set; }
        public string[] Light { get; set; }
        public string[] Dark { get; set; }
        public string[] SurfacesLight { get; set; }
        public string[] SurfacesDark { get; set; }
    }
    public static Dictionary<string, PaletteDefinition> Palettes { get; private set; }
    public static void Init()
    {
        using var paletteStream = Application.GetResourceStream(new Uri("/PlayVoice;component/Resources/Themes/ColorPalettes.json", UriKind.Relative)).Stream;
        Palettes = JsonSerializer.Deserialize<Dictionary<string, PaletteDefinition>>(paletteStream);

        themeList = new()
        {
            new ThemeInfo(ThemeEnum.Light, "浅色",new Uri("Resources/Themes/DefaultLightTheme.xaml", UriKind.Relative)) ,
            new ThemeInfo(ThemeEnum.Dark, "深色", new Uri("Resources/Themes/DefaultDarkTheme.xaml", UriKind.Relative)) ,
            new ThemeInfo(ThemeEnum.System, "系统", null)
        };
        var dict = Application.Current.Resources.MergedDictionaries;
        NowThemeResourceDictionary = new ResourceDictionary();
        dict.Add(NowThemeResourceDictionary);
        LanguageManager.Inst.CultureChanged += (a, b) =>
        {
            foreach (var theme in themeList)
                theme.UpdateName();
        };
    }
    private static ResourceDictionary NowThemeResourceDictionary;
    public static void SwitchTheme(ThemeEnum themeEnum)
    {
        var themeInfo = ThemeList.FirstOrDefault(t => t.Theme == themeEnum) ?? ThemeList[2];
        if (themeInfo.Theme != ThemeEnum.System)
            NowThemeResourceDictionary.Source = themeInfo.Url;
        else
        {
            var systemThemeEnum = GetSystemTheme();
            var systemThemeInfo = ThemeList.FirstOrDefault(t => t.Theme == systemThemeEnum);
            NowThemeResourceDictionary.Source = systemThemeInfo.Url;
        }
        nowNowTheme = themeInfo;
        ApplyPalette();
        ApplyTypographyAndColors();
        GlobalData.Inst.Config.Theme = themeInfo.Theme;
        ThemeChanged?.Invoke(themeInfo);
        GlobalData.Inst.Config.Save();
    }

    public static void SwitchPalette(string name)
    {
        if (!Palettes.ContainsKey(name)) return;
        GlobalData.Inst.Config.ColorPalette = name;
        GlobalData.Inst.Config.Appearance ??= new();
        GlobalData.Inst.Config.Appearance.Accent = null;
        GlobalData.Inst.Config.Appearance.Background = null;
        GlobalData.Inst.Config.Appearance.Foreground = null;
        ApplyPalette();
        ApplyTypographyAndColors();
        GlobalData.Inst.Config.Save();
    }
    private static void ApplyPalette()
    {
        string name = GlobalData.Inst.Config.ColorPalette;
        if (name == null || !Palettes.ContainsKey(name)) name = "Forest";
        bool dark = NowTheme.Theme == ThemeEnum.Dark || (NowTheme.Theme == ThemeEnum.System && GetSystemTheme() == ThemeEnum.Dark);
        var colors = dark ? Palettes[name].Dark : Palettes[name].Light;
        var surfaces = dark ? Palettes[name].SurfacesDark : Palettes[name].SurfacesLight;
        string[] surfaceKeys = { "BgDark", "BgMedium", "BgLight" };
        for (int i = 0; i < surfaceKeys.Length; i++)
            NowThemeResourceDictionary[surfaceKeys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(surfaces[i]));
        NowThemeResourceDictionary["Title"] = NowThemeResourceDictionary["BgMedium"];
        string[] keys = { "AccentColor", "AccentColorSecondary", "AccentSurface", "AccentSurfaceSubtle", "AccentForeground" };
        for (int i = 0; i < keys.Length; i++)
            NowThemeResourceDictionary[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }

    public static void ApplyAppearance(AppearanceProfile profile)
    {
        profile.Validate();
        GlobalData.Inst.Config.Appearance = profile.Copy();
        GlobalData.Inst.Config.ColorPalette = profile.Palette;
        SwitchTheme(Enum.Parse<ThemeEnum>(profile.Mode));
    }

    public static AppearanceProfile CaptureAppearance()
    {
        var result = (GlobalData.Inst.Config.Appearance ?? new()).Copy();
        result.Palette = Palettes.ContainsKey(GlobalData.Inst.Config.ColorPalette ?? "") ? GlobalData.Inst.Config.ColorPalette : "Forest";
        result.Mode = GlobalData.Inst.Config.Theme.ToString();
        return result;
    }

    private static void ApplyTypographyAndColors()
    {
        var profile = GlobalData.Inst.Config.Appearance ?? new();
        try { profile.Validate(); }
        catch (System.IO.InvalidDataException) { profile = new(); GlobalData.Inst.Config.Appearance = profile; }
        var family = Fonts.SystemFontFamilies.Any(font => font.Source == profile.FontFamily)
            ? new FontFamily(profile.FontFamily) : new FontFamily("Segoe UI");
        foreach (string key in new[] { "UiFont", "FontLight", "FontMedium", "FontHeavy" })
            Application.Current.Resources[key] = family;
        for (int size = 9; size <= 40; size++)
            Application.Current.Resources[$"UiFontSize{size}"] = size * profile.FontSize / 13;
        Color Read(string key) => ((SolidColorBrush)NowThemeResourceDictionary[key]).Color;
        void Set(string key, Color color) => NowThemeResourceDictionary[key] = new SolidColorBrush(color);
        Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
        Color Mix(Color a, Color b, double weight) => Color.FromRgb(
            (byte)(a.R * (1 - weight) + b.R * weight),
            (byte)(a.G * (1 - weight) + b.G * weight),
            (byte)(a.B * (1 - weight) + b.B * weight));
        if (!string.IsNullOrEmpty(profile.Background))
        {
            var bg = Parse(profile.Background);
            var fg = bg.R * .299 + bg.G * .587 + bg.B * .114 > 150 ? Colors.Black : Colors.White;
            Set("BgDark", bg); Set("BgMedium", Mix(bg, fg, .035)); Set("BgLight", Mix(bg, fg, .065));
            Set("Title", Read("BgMedium")); Set("TextPrimary", fg);
        }
        if (!string.IsNullOrEmpty(profile.Foreground)) Set("TextPrimary", Parse(profile.Foreground));
        if (!string.IsNullOrEmpty(profile.Accent)) Set("AccentColor", Parse(profile.Accent));
        var accent = Read("AccentColor");
        var background = Read("BgDark");
        Set("AccentSurface", Mix(background, accent, .20));
        Set("AccentSurfaceSubtle", Mix(background, accent, .09));
        Set("AccentColorSecondary", accent);
        Set("AccentForeground", accent.R * .299 + accent.G * .587 + accent.B * .114 > 150 ? Colors.Black : Colors.White);
        Set("TextSecondary", Mix(background, Read("TextPrimary"), .72));
        Set("Border", Mix(background, Read("TextPrimary"), .18));
    }

    public static event Action<ThemeInfo> ThemeChanged;

    public static List<ThemeInfo> ThemeList => themeList;
    private static List<ThemeInfo> themeList;


    public class ThemeInfo
    {
        public ThemeEnum Theme { get; set; }
        public Uri Url { get; set; }
        private string nameKey;
        public string Name { get; set; }
        public int Index => (int)Theme;
        public ThemeInfo(ThemeEnum theme, string nameKey, Uri url)
        {
            Theme = theme;
            this.nameKey = nameKey;
            UpdateName();
            Url = url;
        }
        public void UpdateName()
        {
            Name = LanguageManager.Inst.GetString(nameKey);
        }
    }

    public static ThemeEnum GetSystemTheme()
    {
        const string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string registryValue = "AppsUseLightTheme";
        try
        {
            var value = (int)Registry.GetValue(registryKey, registryValue, 1);
            return value == 0 ? ThemeEnum.Dark : ThemeEnum.Light;
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                exception,
                "读取 Windows 系统主题失败，将使用浅色主题。",
                "ThemeManager.SystemTheme");
            return ThemeEnum.Light;
        }
    }
}
