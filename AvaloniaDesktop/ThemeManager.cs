using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Platform.Storage;
using PlayVoice.Resources.Language;
using System.Text.Json;
namespace PlayVoice.Resources.Themes;

public static class ThemeManager
{
    public enum ThemeEnum { Light, Dark, System }
    public sealed class ThemeInfo(ThemeEnum theme, string name)
    {
        public ThemeEnum Theme { get; } = theme;
        public string Name => LanguageManager.Inst.GetString(name);
        public int Index => (int)Theme;
    }
    public sealed class PaletteDefinition
    {
        public string Name { get; set; }
        public string[] Light { get; set; }
        public string[] Dark { get; set; }
        public string[] SurfacesLight { get; set; }
        public string[] SurfacesDark { get; set; }
    }
    public static List<ThemeInfo> ThemeList { get; } = [new(ThemeEnum.Light, "浅色"), new(ThemeEnum.Dark, "深色"), new(ThemeEnum.System, "系统")];
    public static ThemeInfo NowTheme { get; private set; } = ThemeList[2];
    public static Dictionary<string, PaletteDefinition> Palettes { get; private set; }
    public static event Action<ThemeInfo> ThemeChanged;
    public static void Init()
    {
        using var stream = AssetLoader.Open(new Uri("avares://PlayVoice.Avalonia/Assets/ColorPalettes.json"));
        Palettes = JsonSerializer.Deserialize<Dictionary<string, PaletteDefinition>>(stream)!;
    }
    public static void SelectTheme(ThemeEnum theme)
    {
        GlobalData.Inst.Config.Appearance.UseBuiltInMode(theme.ToString());
        SwitchTheme(theme);
    }
    public static void SwitchTheme(ThemeEnum theme)
    {
        var config = GlobalData.Inst.Config;
        config.Theme = theme;
        config.Appearance.Mode = theme.ToString();
        NowTheme = ThemeList[(int)theme];
        Apply(config.Appearance, config.ColorPalette);
        config.Save();
        ThemeChanged?.Invoke(NowTheme);
    }
    internal static void Apply(AppearanceProfile profile, string palette)
    {
        profile.Validate();
        var app = Application.Current!;
        app.RequestedThemeVariant = profile.Mode switch { "Light" => ThemeVariant.Light, "Dark" => ThemeVariant.Dark, _ => ThemeVariant.Default };
        bool dark = profile.Mode == "Dark" || (profile.Mode == "System" && app.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark);
        var colors = Palettes.GetValueOrDefault(palette) ?? Palettes["Forest"];
        var surfaces = dark ? colors.SurfacesDark : colors.SurfacesLight;
        var accent = Color.Parse(profile.Accent ?? (dark ? colors.Dark[0] : colors.Light[0]));
        var background = Color.Parse(profile.Background ?? surfaces[0]);
        var foreground = Color.Parse(profile.Foreground ?? (dark ? "#F1F3F5" : "#202124"));
        Color Mix(Color a, Color b, double n) => Color.FromRgb((byte)(a.R * (1 - n) + b.R * n), (byte)(a.G * (1 - n) + b.G * n), (byte)(a.B * (1 - n) + b.B * n));
        void Set(string key, Color color) => app.Resources[key] = new SolidColorBrush(color);
        var sidebar = profile.Background == null ? Color.Parse(surfaces[1]) : Mix(background, foreground, .035);
        var surface = profile.Background == null ? Color.Parse(surfaces[2]) : Mix(background, foreground, .065);
        if (profile.SurfaceStyle == "Tinted")
        {
            background = Mix(background, accent, dark ? .08 : .025);
            sidebar = Mix(sidebar, accent, dark ? .18 : .10);
            surface = Mix(surface, accent, dark ? .10 : .04);
        }
        if (profile.SurfaceStyle == "Contrast")
        {
            background = dark ? Color.Parse("#101114") : Colors.White;
            sidebar = dark ? Color.Parse("#202329") : Color.Parse("#EBEDF0");
            surface = dark ? Color.Parse("#292D34") : Colors.White;
        }
        Set("BgDark", background);
        Set("BgMedium", string.IsNullOrEmpty(profile.SidebarBackground) ? sidebar : Color.Parse(profile.SidebarBackground));
        Set("BgLight", string.IsNullOrEmpty(profile.SurfaceBackground) ? surface : Color.Parse(profile.SurfaceBackground));
        Set("TextPrimary", foreground);
        Set("TextSecondary", string.IsNullOrEmpty(profile.SecondaryForeground) ? Mix(background, foreground, .72) : Color.Parse(profile.SecondaryForeground));
        Set("Border", string.IsNullOrEmpty(profile.BorderColor) ? Mix(background, foreground, profile.SurfaceStyle == "Contrast" ? .32 : .14) : Color.Parse(profile.BorderColor));
        Set("AccentColor", accent); Set("AccentSurface", Mix(background, accent, dark ? .25 : .14));
        app.Resources["CardRadius"] = new CornerRadius(profile.CornerRadius);
        app.Resources["ControlRadius"] = new CornerRadius(profile.CornerRadius * 2 / 3);
        app.Resources["ControlHeight"] = profile.ControlHeight;
        app.Resources["RowHeight"] = profile.RowHeight;
        app.Resources["SystemAccentColor"] = accent;
        app.Resources["UiFont"] = new FontFamily(profile.FontFamily);
        app.Resources["UiFontSize"] = profile.FontSize;
    }
    public static void ApplyAppearance(AppearanceProfile profile)
    {
        profile.Validate(); GlobalData.Inst.Config.Appearance = profile.Copy(); GlobalData.Inst.Config.ColorPalette = profile.Palette;
        SwitchTheme(Enum.Parse<ThemeEnum>(profile.Mode));
    }
    public static AppearanceProfile CaptureAppearance()
    {
        var profile = GlobalData.Inst.Config.Appearance.Copy(); profile.Mode = GlobalData.Inst.Config.Theme.ToString(); profile.Palette = GlobalData.Inst.Config.ColorPalette; return profile;
    }
}
