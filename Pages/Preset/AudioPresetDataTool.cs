using NAudio.Wave;
using PlayVoice.Audio;
using System.IO;

namespace PlayVoice.Pages.Preset;

public static class AudioPresetDataTool
{
    public const string ConfigFileName = "AudioPresetConfig.json";
    public static readonly string BasePath = PresetStorage.BasePath;

    public static async Task<AudioPresetData> LoadAudioPresetData(string name)
    {
        var config = FindAudioPresetConfig(name);
        return config == null
            ? null
            : await LoadAudioPresetDataFromPath(BasePath, config.Name);
    }

    public static async Task<AudioPresetData> LoadAudioPresetDataFromPath(string folderPath, string folderName)
    {
        string path = Path.Combine(folderPath, folderName);
        string configPath = Path.Combine(path, ConfigFileName);

        if (!JsonTool.LoadJson(configPath, out AudioPresetDataConfig presetConfigData)
            || presetConfigData == null)
            return null;

        presetConfigData.AudioDataConfigList ??= new();
        var presetData = new AudioPresetData { Config = presetConfigData };
        for (int i = 0; i < presetConfigData.AudioDataConfigList.Count; i++)
        {
            var audioDataConfig = presetConfigData.AudioDataConfigList[i];
            string audioPath = Path.Combine(path, audioDataConfig.Name);
            if (File.Exists(audioPath))
            {
                var audioData = new AudioData
                {
                    Index = i,
                    AudioPreset = presetData
                };
                audioData.AudioTrackArray[0] = new AudioFileReader(audioPath);
                audioData.AudioTrackArray[1] = new AudioFileReader(audioPath);
                presetData.AudioList.Add(audioData);
            }
            else
            {
                presetConfigData.AudioDataConfigList.RemoveAt(i);
                i--;
            }
            await Task.Delay(1);
        }
        return presetData;
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
