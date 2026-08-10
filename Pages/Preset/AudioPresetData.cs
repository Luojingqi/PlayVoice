using NAudio.Wave;
using PlayVoice.Audio;
using System.IO;

namespace PlayVoice.Pages.Preset;

public class AudioPresetData
{
    public List<AudioData> AudioList { get; } = new();

    public AudioPresetDataConfig Config { get; set; }

    public bool Save()
    {
        string path = Path.Combine(AudioPresetDataTool.BasePath, Config.Name);
        return Directory.Exists(path)
            && JsonTool.SaveJson(Path.Combine(path, AudioPresetDataTool.ConfigFileName), Config);
    }

    public void SwapOrder(int index0, int index1)
    {
        AudioList[index0].Index = index1;
        AudioList[index1].Index = index0;

        (AudioList[index0], AudioList[index1]) = (AudioList[index1], AudioList[index0]);
        (Config.AudioDataConfigList[index0], Config.AudioDataConfigList[index1]) =
            (Config.AudioDataConfigList[index1], Config.AudioDataConfigList[index0]);
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
            using var sourceReader = new AudioFileReader(completePath);
            if (sourceReader.Length <= 0 || sourceReader.TotalTime.TotalMilliseconds <= 0)
            {
                Console.WriteLine("音频文件长度或时长异常。");
                return null;
            }

            double? actualLufs = await AudioData.MeasureLufs(completePath);
            if (!actualLufs.HasValue)
                return null;

            string presetPath = Path.Combine(AudioPresetDataTool.BasePath, Config.Name);
            string destPath = Path.Combine(presetPath, Path.GetFileName(completePath));
            if (File.Exists(destPath))
            {
                Console.WriteLine("音频文件已存在。");
                return null;
            }

            File.Copy(completePath, destPath, false);
            var audioData = new AudioData
            {
                Index = AudioList.Count,
                AudioPreset = this
            };
            audioData.AudioTrackArray[0] = new AudioFileReader(destPath);
            audioData.AudioTrackArray[1] = new AudioFileReader(destPath);
            AudioList.Add(audioData);

            var audioDataConfig = new AudioDataConfig
            {
                FileName = Path.GetFileNameWithoutExtension(completePath),
                FileFormat = Path.GetExtension(completePath),
                Lufs = actualLufs.Value,
                Size = new FileInfo(destPath).Length
            };
            Console.WriteLine($"{audioDataConfig.Name} 实际LUFS: {actualLufs}");
            Config.AudioDataConfigList.Add(audioDataConfig);
            return audioData;
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
        string audioId = audioData.Config.Id;
        string filePath = Path.Combine(
            AudioPresetDataTool.BasePath, Config.Name, audioData.Config.Name);
        if (!File.Exists(filePath))
            return false;

        audioData.Dispose();
        File.Delete(filePath);
        AudioList.RemoveAt(index);
        Config.AudioDataConfigList.RemoveAt(index);
        for (int i = index; i < AudioList.Count; i++)
            AudioList[i].Index = i;

        GlobalData.Inst.RemoveAudioBinding(Config.Name, audioId);
        return true;
    }

    public void Dispose()
    {
        foreach (var audioData in AudioList)
            audioData.Dispose();
    }
}
