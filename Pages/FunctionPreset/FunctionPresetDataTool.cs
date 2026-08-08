using System.IO;

namespace PlayVoice.Pages.FunctionPreset;

public static class FunctionPresetDataTool
{
    public static readonly string BasePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "FunctionPresets");

    public static List<FunctionPresetData> GetAll()
    {
        Directory.CreateDirectory(BasePath);
        var presets = new List<FunctionPresetData>();
        foreach (var path in Directory.GetFiles(BasePath, "*.json"))
        {
            if (JsonTool.LoadJson(path, out FunctionPresetData preset) && preset != null)
            {
                preset.Bindings ??= new();
                preset.FeatureData ??= new();
                foreach (var binding in preset.Bindings)
                    binding.HotkeyData ??= new();
                presets.Add(preset);
            }
        }
        return presets.OrderBy(preset => preset.DisplayName).ToList();
    }

    public static FunctionPresetData Load(string id) =>
        GetAll().FirstOrDefault(preset => preset.Id == id);

    public static FunctionPresetData Create(string name)
    {
        string normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
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
        if (preset == null || string.IsNullOrWhiteSpace(preset.Id))
            return false;
        Directory.CreateDirectory(BasePath);
        return JsonTool.SaveJson(Path.Combine(BasePath, $"{preset.Id}.json"), preset);
    }

    public static FunctionPresetData Copy(FunctionPresetData source, string name)
    {
        string normalizedName = name?.Trim();
        if (source == null || string.IsNullOrWhiteSpace(normalizedName))
            return null;
        if (GetAll().Any(preset =>
            string.Equals(preset.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            return null;

        var copy = JsonTool.ToObject<FunctionPresetData>(JsonTool.ToJson(source));
        if (copy == null)
            return null;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = normalizedName;
        copy.IsDefault = false;
        return Save(copy) ? copy : null;
    }

    public static bool Rename(FunctionPresetData preset, string name)
    {
        string normalizedName = name?.Trim();
        if (preset == null || preset.IsDefault || string.IsNullOrWhiteSpace(normalizedName))
            return false;
        if (GetAll().Any(item => item.Id != preset.Id
            && string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            return false;

        preset.Name = normalizedName;
        return Save(preset);
    }

    public static bool Delete(string id)
    {
        var preset = GetAll().FirstOrDefault(item => item.Id == id);
        if (preset == null || preset.IsDefault)
            return false;

        string path = Path.Combine(BasePath, $"{id}.json");
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    public static void RemoveAudioBindings(string audioPresetId, string audioId)
    {
        foreach (var preset in GetAll())
        {
            if (preset.RemoveHotkey(audioPresetId, audioId))
                Save(preset);
        }
    }

    public static void RemoveAudioPresetBindings(string audioPresetId)
    {
        foreach (var preset in GetAll())
        {
            if (preset.RemoveAudioPresetBindings(audioPresetId))
                Save(preset);
        }
    }

    public static FunctionPresetData EnsureCurrent(string currentId)
    {
        var presets = GetAll();
        if (presets.Count == 0)
            return CreateDefault();
        return presets.FirstOrDefault(preset => preset.Id == currentId)
            ?? presets.FirstOrDefault(preset => preset.IsDefault)
            ?? presets[0];
    }
}
