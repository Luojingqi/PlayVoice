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
    public string Accent { get; set; }
    public string Background { get; set; }
    public string Foreground { get; set; }

    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 80
            || !double.IsFinite(FontSize) || FontSize < 11 || FontSize > 18
            || FontFamily == null || !Regex.IsMatch(FontFamily, @"^[\p{L}\p{N} ._-]{1,100}$")
            || Mode is not ("Light" or "Dark" or "System")
            || Palette is not ("Forest" or "Ocean" or "Amber" or "Rose"))
            throw new InvalidDataException("Invalid theme profile.");
        foreach (var color in new[] { Accent, Background, Foreground })
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
