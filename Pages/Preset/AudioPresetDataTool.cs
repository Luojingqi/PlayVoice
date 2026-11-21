using NAudio.Wave;
using PlayVoice.Audio;
using PlayVoice.Diagnostics;
using System.IO;

namespace PlayVoice.Pages.Preset;

public static class AudioPresetDataTool
{
    public const string ConfigFileName = "AudioPresetConfig.json";
    public static readonly string BasePath = PresetStorage.BasePath;

    public static async Task<AudioPresetData> LoadAudioPresetData(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = FindAudioPresetConfig(name);
        return config == null
            ? null
            : await LoadAudioPresetDataFromPath(
                BasePath,
                config.Name,
                cancellationToken);
    }

    public static async Task<AudioPresetData> LoadAudioPresetDataFromPath(
        string folderPath,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!PresetStorage.TryNormalizeName(folderName, out var safeFolderName) || safeFolderName != folderName)
            throw new InvalidDataException("Invalid audio preset directory name.");
        string path = Path.Combine(folderPath, folderName);
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked preset directories are not supported.");
        string configPath = Path.Combine(path, ConfigFileName);

        if (!JsonTool.LoadJson(configPath, out AudioPresetDataConfig presetConfigData)
            || presetConfigData == null)
            return null;

        presetConfigData.AudioDataConfigList ??= new();
        if (presetConfigData.Version != 1) throw new NotSupportedException("Unsupported audio preset version.");
        if (presetConfigData.AudioDataConfigList.Count > GlobalData.Inst.Config.AudioPresetMaxFiles)
            throw new InvalidDataException("Audio preset contains too many files.");
        var presetData = new AudioPresetData { Config = presetConfigData };
        try
        {
            for (int i = 0; i < presetConfigData.AudioDataConfigList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var audioDataConfig = presetConfigData.AudioDataConfigList[i];
                if (audioDataConfig == null || string.IsNullOrEmpty(audioDataConfig.Name)
                    || Path.GetFileName(audioDataConfig.Name) != audioDataConfig.Name
                    || audioDataConfig.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidDataException("Invalid audio file name.");
                string audioPath = Path.Combine(path, audioDataConfig.Name);
                if (File.Exists(audioPath) && ((File.GetAttributes(audioPath) & FileAttributes.ReparsePoint) != 0
                    || new FileInfo(audioPath).Length > GlobalData.Inst.Config.AudioImportMaxBytes))
                    throw new InvalidDataException("Audio file is linked or exceeds the configured size limit.");
                if (File.Exists(audioPath))
                {
                    var audioData = new AudioData
                    {
                        Index = i,
                        AudioPreset = presetData
                    };
                    try
                    {
                        audioData.AudioTrackArray[0] = new AudioFileReader(audioPath);
                        audioData.AudioTrackArray[1] = new AudioFileReader(audioPath);
                        cancellationToken.ThrowIfCancellationRequested();
                        presetData.AudioList.Add(audioData);
                    }
                    catch (Exception)
                    {
                        audioData.AudioTrackArray[0]?.Dispose();
                        audioData.AudioTrackArray[1]?.Dispose();
                        audioData.PlayTimer.Stop();
                        throw;
                    }
                }
                else
                {
                    throw new FileNotFoundException("Audio preset references a missing file; its manifest was left unchanged.", audioPath);
                }
                await Task.Delay(1, cancellationToken);
            }
            return presetData;
        }
        catch (OperationCanceledException)
        {
            presetData.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            presetData.Dispose();
            AppLog.Error(
                exception,
                $"加载音频预设失败：{folderName}",
                "AudioPreset.Load");
            throw;
        }
    }

    public static bool CreateAudioPresetData(string name, out AudioPresetData presetData)
    {
        if (!PresetStorage.TryNormalizeName(name, out string normalizedName))
        {
            presetData = null;
            return false;
        }

        if (GetAllAudioPresetConfigs().Any(item =>
            string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            MainWindow.Inst.AddNotification(
                () => Resources.Language.LanguageManager.Inst.GetString("音频预设已存在"),
                () => Resources.Language.LanguageManager.Inst.SpliceString(
                    Resources.Language.LanguageManager.Inst.GetString("名称已存在"), normalizedName),
                Pages.LabelStatus.Warning);
            presetData = null;
            return false;
        }

        var config = new AudioPresetDataConfig { Name = normalizedName };
        string path = Path.Combine(BasePath, config.Name);
        Directory.CreateDirectory(path);
        presetData = new AudioPresetData { Config = config };
        if (presetData.Save())
            return true;

        presetData = null;
        return false;
    }

    public static bool DeleteAudioPresetData(string name)
    {
        var config = FindAudioPresetConfig(name);
        if (config == null)
            return false;

        string path = Path.Combine(BasePath, config.Name);
        if (!Directory.Exists(path))
            return false;

        if (string.Equals(
            GlobalData.Inst.ActiveAudioPreset?.Config.Name,
            config.Name,
            StringComparison.OrdinalIgnoreCase))
            GlobalData.Inst.ActiveAudioPreset = null;
        Directory.Delete(path, true);
        GlobalData.Inst.RemoveAudioPresetBindings(config.Name);
        return true;
    }

    public static string[] GetAllAudioPresetName() =>
        GetAllAudioPresetConfigs().Select(item => item.Name).ToArray();

    public static List<AudioPresetDataConfig> GetAllAudioPresetConfigs()
    {
        PresetStorage.EnsureInitialized();
        var result = new List<AudioPresetDataConfig>();
        foreach (string directory in Directory.GetDirectories(BasePath))
        {
            string configPath = Path.Combine(directory, ConfigFileName);
            if (JsonTool.LoadJson(configPath, out AudioPresetDataConfig config) && config != null)
            {
                config.AudioDataConfigList ??= new();
                result.Add(config);
            }
        }
        return result.OrderBy(item => item.Name).ToList();
    }

    public static AudioPresetDataConfig FindAudioPresetConfig(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return GetAllAudioPresetConfigs().FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
