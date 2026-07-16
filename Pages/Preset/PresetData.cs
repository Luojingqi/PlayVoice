using NAudio.Wave;
using PlayVoice.Audio;
using PlayVoice.Hotkey;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace PlayVoice.Pages.Preset;

public class PresetData
{
    public List<AudioData> AudioList = new();

    public PresetDataConfig Config;


    public bool Save()
    {
        string path = Path.Combine(PresetDataTool.basePath, Config.Name);
        if (!Directory.Exists(path))
        {
            return false;
        }
        else
        {
            return JsonTool.SaveJson(Path.Combine(path, "PresetConfig.json"), Config);
        }
    }

    public void SwapOrder(int index0, int index1)
    {
        {
            AudioList[index0].Index = index1;
            AudioList[index1].Index = index0;
        }
        {
            var temp = AudioList[index0];
            AudioList[index0] = AudioList[index1];
            AudioList[index1] = temp;
        }
        {
            var temp = Config.AudioDataConfigList[index0];
            Config.AudioDataConfigList[index0] = Config.AudioDataConfigList[index1];
            Config.AudioDataConfigList[index1] = temp;
        }
    }
    public void ChangeName(string newName)
    {
        //还需改变文件夹名称
        Config.Name = newName;
    }

    public async Task<AudioData> AddAudio(string completePath)
    {
        if (string.IsNullOrEmpty(completePath) || !File.Exists(completePath))
        {
            Console.WriteLine("文件不存在。");
            return null;
        }

        try
        {
            var reader = new AudioFileReader(completePath);
            if (reader.Length <= 0 || reader.TotalTime.TotalMilliseconds <= 0)
            {
                Console.WriteLine("音频文件长度或时长异常。");
                reader.Dispose();
                return null;
            }

            string destPath = Path.Combine(PresetDataTool.basePath, Config.Name, Path.GetFileName(completePath));

            if (!File.Exists(destPath))
            {
                File.Copy(completePath, destPath, false);
                var audioData = new AudioData
                {
                    Index = AudioList.Count,
                    Preset = this
                };

                audioData.AudioTrackArray[0] = reader;
                audioData.AudioTrackArray[1] = new AudioFileReader(completePath);
                AudioList.Add(audioData);
                var audioDataConfig = new AudioDataConfig
                {
                    FileName = Path.GetFileNameWithoutExtension(completePath),
                    FileFormat = Path.GetExtension(completePath),
                    HotkeyData = new HotkeyData()
                };
                using (var fs = System.IO.File.OpenRead(destPath))
                {
                    audioDataConfig.Size = fs.Length;
                }
                double actualLufs = await AudioData.MeasureLufs(destPath);
                double lufsDifference = AudioData.TargetLufs - actualLufs;
                audioDataConfig.Decibel = lufsDifference;
                Console.WriteLine($"{audioDataConfig.Name} 实际LUFS: {actualLufs}");
                Config.AudioDataConfigList.Add(audioDataConfig);
                return audioData;
            }
            else
            {
                Console.WriteLine("音频文件已存在。");
                reader.Dispose();
                return null;
            }
        }
        catch
        {
            Console.WriteLine("无法读取音频文件。");
            return null;
        }
    }


    public bool RemoveAudio(int index)
    {
        if (index < 0 || index >= AudioList.Count)
        {
            Console.WriteLine("索引超出范围。");
            return false;
        }
        var audioData = AudioList[index];
        string filePath = Path.Combine(PresetDataTool.basePath, Config.Name, audioData.Config.Name);
        if (File.Exists(filePath))
        {
            AudioList[index].Dispose();
            File.Delete(filePath);
            AudioList.RemoveAt(index);
            Config.AudioDataConfigList.RemoveAt(index);
            for (int i = index; i < AudioList.Count; i++)
            {
                AudioList[i].Index = i;
            }
            return true;
        }
        else
        { return false; }

    }

    public void Dispose()
    {
        foreach (var audioData in AudioList)
        {
            audioData.Dispose();
        }
    }
}