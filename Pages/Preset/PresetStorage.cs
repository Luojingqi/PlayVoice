using PlayVoice.Diagnostics;
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

    public static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long totalSize = 0;
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (string filePath in Directory.EnumerateFiles(path, "*", enumerationOptions))
        {
            totalSize = checked(totalSize + new FileInfo(filePath).Length);
        }

        return totalSize;
    }

    /// <summary>
    /// 将当前 ExtendedExplanation 归档为递增编号目录，并创建新的空工作目录。
    /// </summary>
    public static string ArchiveExtendedExplanation()
    {
        Directory.CreateDirectory(ResourcesPath);
        Directory.CreateDirectory(ExtendedExplanationPath);

        const string directoryName = "ExtendedExplanation";
        long nextIndex = 1;
        foreach (string path in Directory.EnumerateDirectories(
                     ResourcesPath,
                     $"{directoryName}*",
                     SearchOption.TopDirectoryOnly))
        {
            string existingName = Path.GetFileName(path);
            if (existingName.Length <= directoryName.Length)
                continue;

            string suffix = existingName[directoryName.Length..];
            if (long.TryParse(suffix, out long index) && index >= nextIndex)
                nextIndex = checked(index + 1);
        }

        string archivePath;
        do
        {
            archivePath = Path.Combine(ResourcesPath, $"{directoryName}{nextIndex}");
            nextIndex = checked(nextIndex + 1);
        }
        while (Directory.Exists(archivePath) || File.Exists(archivePath));

        Directory.Move(ExtendedExplanationPath, archivePath);
        try
        {
            Directory.CreateDirectory(ExtendedExplanationPath);
        }
        catch (Exception exception)
        {
            try
            {
                if (!Directory.Exists(ExtendedExplanationPath) && Directory.Exists(archivePath))
                    Directory.Move(archivePath, ExtendedExplanationPath);
            }
            catch (Exception rollbackException)
            {
                AppLog.Error(
                    rollbackException,
                    "恢复扩展说明目录失败。",
                    "PresetStorage.Archive");
            }
            AppLog.Error(
                exception,
                "创建新的扩展说明目录失败。",
                "PresetStorage.Archive");
            throw;
        }

        return archivePath;
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
