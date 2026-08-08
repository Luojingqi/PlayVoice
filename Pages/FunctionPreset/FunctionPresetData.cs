using PlayVoice.Hotkey;
using PlayVoice.Resources.Language;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayVoice.Pages.FunctionPreset;

public class FunctionPresetData
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public List<AudioHotkeyBinding> Bindings { get; set; } = new();
    public Dictionary<string, JsonElement> FeatureData { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => IsDefault
        ? LanguageManager.Inst.GetString("默认")
        : Name;

    public HotkeyData GetHotkey(string audioPresetId, string audioId, bool create)
    {
        var binding = Bindings.FirstOrDefault(item =>
            item.AudioPresetId == audioPresetId && item.AudioId == audioId);
        if (binding != null)
            return binding.HotkeyData;

        if (!create)
            return null;

        binding = new AudioHotkeyBinding
        {
            AudioPresetId = audioPresetId,
            AudioId = audioId,
        };
        Bindings.Add(binding);
        return binding.HotkeyData;
    }

    public bool RemoveHotkey(string audioPresetId, string audioId) =>
        Bindings.RemoveAll(item =>
            item.AudioPresetId == audioPresetId && item.AudioId == audioId) > 0;

    public bool RemoveAudioPresetBindings(string audioPresetId) =>
        Bindings.RemoveAll(item => item.AudioPresetId == audioPresetId) > 0;

    public bool Save() => FunctionPresetDataTool.Save(this);
}

public class AudioHotkeyBinding
{
    public string AudioPresetId { get; set; }
    public string AudioId { get; set; }
    public HotkeyData HotkeyData { get; set; } = new();
}
