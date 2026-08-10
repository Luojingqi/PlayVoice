using PlayVoice.Hotkey;
using PlayVoice.Pages.Preset;
using PlayVoice.Pages.Workshop;
using System.IO;

namespace PlayVoice.Pages.FunctionPreset;

public static class FunctionPresetDataTool
{
    public static readonly string BasePath = PresetStorage.BasePath;

    public static List<FunctionPresetData> GetAll()
    {
        PresetStorage.EnsureInitialized();
        var presets = new List<FunctionPresetData>();
        foreach (var path in Directory.GetFiles(BasePath, "*.json"))
        {
            if (JsonTool.LoadJson(path, out FunctionPresetData preset) && preset != null)
            {
                preset.Bindings ??= new();
                preset.FeatureData ??= new();
                foreach (var binding in preset.Bindings)
                    binding.HotkeyData ??= new();
                preset.BeforePlayingKey ??= new()
                {
                    Action = PlayAudioKeyData.KeyAction.按下
                };
                preset.BeforePlayingKey.HotkeyData ??= new();
                preset.AfterPlayingKey ??= new()
                {
                    Action = PlayAudioKeyData.KeyAction.抬起
                };
                preset.AfterPlayingKey.HotkeyData ??= new();
                presets.Add(preset);
            }
        }
        return presets.OrderBy(preset => preset.DisplayName).ToList();
    }

    public static FunctionPresetData Load(string name) =>
        GetAll().FirstOrDefault(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    public static FunctionPresetData Create(string name)
    {
        if (!PresetStorage.TryNormalizeName(name, out string normalizedName))
            return null;
        if (GetAll().Any(preset =>
            string.Equals(preset.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            return null;

        var preset = new FunctionPresetData { Name = normalizedName };
        return Save(preset) ? preset : null;
    }

    public static FunctionPresetData CreateDefault()
    {
        var existingDefault = GetAll().FirstOrDefault(preset => preset.IsDefault);
        if (existingDefault != null)
            return existingDefault;

        var preset = new FunctionPresetData
        {
            Name = "Default",
            IsDefault = true
        };
        return Save(preset) ? preset : null;
    }

    public static bool Save(FunctionPresetData preset)
    {
        if (preset == null
            || !PresetStorage.TryNormalizeName(preset.Name, out string normalizedName))
            return false;
        preset.Name = normalizedName;
        Directory.CreateDirectory(BasePath);
        return JsonTool.SaveJson(Path.Combine(BasePath, $"{preset.Name}.json"), preset);
    }

    public static FunctionPresetData Copy(FunctionPresetData source, string name)
    {
        if (source == null
            || !PresetStorage.TryNormalizeName(name, out string normalizedName))
            return null;
        if (GetAll().Any(preset =>
            string.Equals(preset.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            return null;

        var copy = JsonTool.ToObject<FunctionPresetData>(JsonTool.ToJson(source));
        if (copy == null)
            return null;
        copy.Name = normalizedName;
        copy.IsDefault = false;
        return Save(copy) ? copy : null;
    }

    public static bool Rename(FunctionPresetData preset, string name)
    {
        if (preset == null
            || preset.IsDefault
            || !PresetStorage.TryNormalizeName(name, out string normalizedName))
            return false;
        string oldName = preset.Name;
        if (!string.Equals(oldName, normalizedName, StringComparison.OrdinalIgnoreCase)
            && GetAll().Any(item => string.Equals(
                item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            return false;

        preset.Name = normalizedName;
        if (!Save(preset))
        {
            preset.Name = oldName;
            return false;
        }

        string oldPath = Path.Combine(BasePath, $"{oldName}.json");
        string newPath = Path.Combine(BasePath, $"{normalizedName}.json");
        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(oldPath))
            File.Delete(oldPath);

        var globalData = GlobalData.Inst;
        if (globalData != null && string.Equals(
            globalData.Config.ActiveFunctionPresetName,
            oldName,
            StringComparison.OrdinalIgnoreCase))
        {
            globalData.Config.ActiveFunctionPresetName = normalizedName;
            globalData.Config.Save();
        }
        return true;
    }

    public static bool Delete(string name)
    {
        var preset = Load(name);
        if (preset == null || preset.IsDefault)
            return false;

        string path = Path.Combine(BasePath, $"{preset.Name}.json");
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    public static void RemoveAudioBindings(string audioPresetName, string audioId)
    {
        foreach (var preset in GetAll())
        {
            if (preset.RemoveAudioBinding(audioPresetName, audioId))
                Save(preset);
        }
    }

    public static void RemoveAudioPresetBindings(string audioPresetName)
    {
        foreach (var preset in GetAll())
        {
            if (preset.RemoveAudioPresetBindings(audioPresetName))
                Save(preset);
        }
    }

    public static void RenameAudioPresetBindings(string oldName, string newName)
    {
        foreach (var preset in GetAll())
        {
            if (preset.RenameAudioPresetBindings(oldName, newName))
                Save(preset);
        }
    }

    public static FunctionPresetData EnsureCurrent(string currentName)
    {
        var presets = GetAll();
        if (presets.Count == 0)
            return CreateDefault();
        return presets.FirstOrDefault(preset => string.Equals(
                preset.Name, currentName, StringComparison.OrdinalIgnoreCase))
            ?? presets.FirstOrDefault(preset => preset.IsDefault)
            ?? presets[0];
    }

}
