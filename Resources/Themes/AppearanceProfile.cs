using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlayVoice.Resources.Themes;

public sealed class AppearanceProfile
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Custom";
    public string Palette { get; set; } = "Forest";
    public string Mode { get; set; } = "System";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 13;
    public string WindowMaterial { get; set; } = "Acrylic";
    public double MaterialOpacity { get; set; } = .84;
    public string SurfaceStyle { get; set; } = "Neutral";
    public string SurfaceBackground { get; set; }
    public string SidebarBackground { get; set; }
    public string SecondaryForeground { get; set; }
    public string BorderColor { get; set; }
    public double CornerRadius { get; set; } = 12;
    public double ControlHeight { get; set; } = 38;
    public double RowHeight { get; set; } = 52;
    public string Accent { get; set; }
    public string Background { get; set; }
    public string Foreground { get; set; }

    public void UseBuiltInMode(string mode)
    {
        if (mode is not ("Light" or "Dark" or "System")) throw new ArgumentException("Invalid theme mode.", nameof(mode));
        Mode = mode;
        Background = null;
        Foreground = null;
        SurfaceBackground = SidebarBackground = SecondaryForeground = BorderColor = null;
    }

    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80
            || !double.IsFinite(FontSize) || FontSize < 11 || FontSize > 18
            || !double.IsFinite(MaterialOpacity) || MaterialOpacity < .6 || MaterialOpacity > 1
            || SurfaceStyle is not ("Neutral" or "Tinted" or "Contrast")
            || !double.IsFinite(CornerRadius) || CornerRadius < 0 || CornerRadius > 24
            || !double.IsFinite(ControlHeight) || ControlHeight < 34 || ControlHeight > 52
            || !double.IsFinite(RowHeight) || RowHeight < 44 || RowHeight > 72
            || WindowMaterial is not ("Solid" or "Acrylic" or "Mica")
            || FontFamily == null || !Regex.IsMatch(FontFamily, @"^[\p{L}\p{N} ._-]{1,100}$")
            || Mode is not ("Light" or "Dark" or "System")
            || Palette is not ("Forest" or "Ocean" or "Amber" or "Rose"))
            throw new InvalidDataException("Invalid theme profile.");
        foreach (var color in new[] { Accent, Background, Foreground, SurfaceBackground, SidebarBackground, SecondaryForeground, BorderColor })
            if (!string.IsNullOrEmpty(color) && !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$"))
                throw new InvalidDataException("Colors must use #RRGGBB.");
        if (!string.IsNullOrEmpty(Background) && string.Equals(Background, Foreground, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Text and background colors must differ.");
    }

    public static AppearanceProfile Parse(string json)
    {
        if (json.Length > 65536) throw new InvalidDataException("Theme file is too large.");
        var profile = JsonSerializer.Deserialize<AppearanceProfile>(json, new JsonSerializerOptions { MaxDepth = 8 })
            ?? throw new InvalidDataException("Theme file is empty.");
        profile.Validate();
        return profile;
    }

    public AppearanceProfile Copy() => Parse(JsonSerializer.Serialize(this));
}
