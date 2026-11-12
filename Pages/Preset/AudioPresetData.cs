using NAudio.Wave;
using PlayVoice.Audio;
using PlayVoice.Diagnostics;
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
            && JsonTool.SaveJsonAtomically(Path.Combine(path, AudioPresetDataTool.ConfigFileName), Config);
    }

    public void SwapOrder(int index0, int index1)
    {
        var audio = AudioList[index0];
        var config = Config.AudioDataConfigList[index0];
        AudioList.RemoveAt(index0);
        Config.AudioDataConfigList.RemoveAt(index0);
        AudioList.Insert(index1, audio);
        Config.AudioDataConfigList.Insert(index1, config);
        for (int i = 0; i < AudioList.Count; i++) AudioList[i].Index = i;
        NotifyTracksChanged();
    }

    public void RenameAudio(AudioData audio, string newName)
    {
        if (!AudioList.Contains(audio)) throw new InvalidOperationException();
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || newName.EndsWith('.') || newName is "." or ".."
            || System.Text.RegularExpressions.Regex.IsMatch(newName.Split('.')[0],
                @"^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new ArgumentException(Resources.Language.LanguageManager.Inst.GetString("名称无效"));
        var config = audio.Config;
        string oldName = config.FileName;
        if (newName == oldName) return;
        string folder = Path.Combine(AudioPresetDataTool.BasePath, Config.Name);
        string oldPath = Path.Combine(folder, config.Name);
        string newPath = Path.Combine(folder, newName + config.FileFormat);
        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
            throw new IOException(Resources.Language.LanguageManager.Inst.GetString("文件已存在"));
        audio.Stop();
        foreach (var reader in audio.AudioTrackArray) reader?.Dispose();
        bool moved = false;
        try
        {
            File.Move(oldPath, newPath);
            moved = true;
            config.FileName = newName;
            OpenReaders(audio, newPath);
            if (!Save()) throw new IOException(Resources.Language.LanguageManager.Inst.GetString("保存失败"));
            var copies = GlobalData.Inst.CopyAudioPathList;
            for (int i = 0; i < copies.Count; i++)
                if (string.Equals(copies[i], oldPath, StringComparison.OrdinalIgnoreCase)) copies[i] = newPath;
        }
        catch
        {
            foreach (var reader in audio.AudioTrackArray) reader?.Dispose();
            if (moved) File.Move(newPath, oldPath);
            config.FileName = oldName;
            OpenReaders(audio, oldPath);
            throw;
        }
    }

    private static void OpenReaders(AudioData audio, string path)
    {
        audio.AudioTrackArray[0] = new AudioFileReader(path);
        audio.AudioTrackArray[1] = new AudioFileReader(path);
    }

    public async Task<AudioData> AddAudio(
        string completePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(completePath) || !File.Exists(completePath))
        {
            AppLog.Warning($"导入的音频文件不存在：{completePath}", "AudioPreset.Import");
            return null;
        }

        try
        {
            if (AudioList.Count >= GlobalData.Inst.Config.AudioPresetMaxFiles
                || new FileInfo(completePath).Length > GlobalData.Inst.Config.AudioImportMaxBytes
                || (File.GetAttributes(completePath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Audio import exceeds configured limits or uses a linked file.");
            using var sourceReader = new AudioFileReader(completePath);
            if (sourceReader.Length <= 0 || sourceReader.TotalTime.TotalMilliseconds <= 0)
            {
                AppLog.Warning(
                    $"音频文件长度或时长异常：{completePath}",
                    "AudioPreset.Import");
                return null;
            }

            double? actualLufs = await AudioData.MeasureLufs(completePath, cancellationToken);
            if (!actualLufs.HasValue)
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            string presetPath = Path.Combine(AudioPresetDataTool.BasePath, Config.Name);
            string destPath = Path.Combine(presetPath, Path.GetFileName(completePath));
            if (File.Exists(destPath))
            {
                AppLog.Warning(
                    $"音频预设中已存在同名文件：{destPath}",
                    "AudioPreset.Import");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
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
            AppLog.Information(
                $"音频导入完成。Name={audioDataConfig.Name} | LUFS={actualLufs}",
                "AudioPreset.Import");
            Config.AudioDataConfigList.Add(audioDataConfig);
            NotifyTracksChanged();
            return audioData;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppLog.Error(
                exception,
                $"无法读取音频文件：{completePath}",
                "AudioPreset.Import");
            return null;
        }
    }

    public bool RemoveAudio(int index)
    {
        if (index < 0 || index >= AudioList.Count)
        {
            AppLog.Warning(
                $"删除音频时索引超出范围：{index}",
                "AudioPreset.Remove");
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
        NotifyTracksChanged();
        return true;
    }

    private void NotifyTracksChanged()
    {
        if (GlobalData.Inst?.ActiveAudioPreset == this) GlobalData.Inst.Playback.TracksChanged();
    }

    public void Dispose()
    {
        foreach (var audioData in AudioList)
            audioData.Dispose();
    }
}
