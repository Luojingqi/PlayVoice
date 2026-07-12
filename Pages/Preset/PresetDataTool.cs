using NAudio.Wave;
using PlayVoice.Audio;
using System.IO;

namespace PlayVoice.Pages.Preset;

public static class PresetDataTool
{
    public static readonly string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/Preset/");
    public static async Task<PresetData> LoadPresetData(string name)
    {
        string path = Path.Combine(basePath);
        return await LoadPresetDataFromPath(path, name);
    }

    public static async Task<PresetData> LoadPresetDataFromPath(string folderPath, string name)
    {
        string path = Path.Combine(folderPath, name);
        string configPath = Path.Combine(path, "PresetConfig.json");

        if (JsonTool.LoadJson(configPath, out PresetDataConfig presetConfigData))
        {
            var presetData = new PresetData();
            presetData.Config = presetConfigData;
            for (int i = 0; i < presetConfigData.AudioDataConfigList.Count; i++)
            {
                var audioDataConfig = presetConfigData.AudioDataConfigList[i];
                if (File.Exists(Path.Combine(path, $"{audioDataConfig.FileName}{audioDataConfig.FileFormat}")))
                {
                    var audioData = new AudioData();
                    var audioPath = Path.Combine(path, $"{audioDataConfig.FileName}{audioDataConfig.FileFormat}");
                    audioData.AudioTrackArray[0] = new AudioFileReader(audioPath);
                    audioData.AudioTrackArray[1] = new AudioFileReader(audioPath);
                    audioData.Index = i;
                    audioData.Preset = presetData;
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
        else
        {
            return null;
        }
    }

    public static bool CreatePresetData(string name, out PresetData presetData)
    {
        string path = Path.Combine(basePath, name);
        Console.WriteLine(path);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            presetData = new PresetData();
            presetData.Config = new PresetDataConfig();
            presetData.Config.Name = name;
            JsonTool.SaveJson(Path.Combine(path, "PresetConfig.json"), presetData.Config);
            return true;
        }
        else
        {
            MainWindow.Inst.AddNotification("预设已存在", $"文件夹 {name} 已存在", Pages.LabelStatus.Warning);
            presetData = null;
            return false;
        }
    }

    public static void DeletePresetData(string presetName)
    {
        string path = Path.Combine(basePath, presetName);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }


    public static string[] GetAllPresetName()
    {
        return Directory.GetDirectories(Path.Combine(basePath))
                        .Where(dir => File.Exists(Path.Combine(dir, "PresetConfig.json")))
                        .Select(Path.GetFileName)
                        .ToArray();
    }
}
