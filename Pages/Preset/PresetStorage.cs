using System.IO;

namespace PlayVoice.Pages.Preset;

internal static class PresetStorage
{
    private static readonly HashSet<string> ReservedFileNames = new(
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
            "LPT6", "LPT7", "LPT8", "LPT9"
        },
        StringComparer.OrdinalIgnoreCase);

    public static readonly string ResourcesPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources");
    public static readonly string BasePath = Path.Combine(ResourcesPath, "Preset");
    public static readonly string TempPath = Path.Combine(ResourcesPath, "temp");
    public static readonly string ExtendedExplanationPath = Path.Combine(
        ResourcesPath, "ExtendedExplanation");

    public static void EnsureInitialized()
    {
        Directory.CreateDirectory(ResourcesPath);
        Directory.CreateDirectory(BasePath);
        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(ExtendedExplanationPath);
    }

    public static bool TryNormalizeName(string name, out string normalizedName)
    {
        normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName)
            || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalizedName.EndsWith('.')
            || ReservedFileNames.Contains(
                Path.GetFileNameWithoutExtension(normalizedName)))
        {
            normalizedName = null;
            return false;
        }

        return true;
    }
}
