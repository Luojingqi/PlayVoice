using PlayVoice.Hotkey;
using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Language;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayVoice.Pages.FunctionPreset;

public class FunctionPresetData
{
    public int SchemaVersion { get; set; } = 3;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public List<FunctionPresetAudioBinding> Bindings { get; set; } = new();
    public Dictionary<string, JsonElement> FeatureData { get; set; } = new();
    public PlayAudioKeyData BeforePlayingKey { get; set; } = new()
    {
        Action = PlayAudioKeyData.KeyAction.按下
    };
    public PlayAudioKeyData AfterPlayingKey { get; set; } = new()
    {
        Action = PlayAudioKeyData.KeyAction.抬起
    };

    [JsonIgnore]
    public string DisplayName => IsDefault
        ? LanguageManager.Inst.GetString("默认")
        : Name;

    public HotkeyData GetHotkey(string audioPresetId, string audioId, bool create)
    {
        var binding = GetAudioBinding(audioPresetId, audioId, create);
        if (binding != null)
            return binding.HotkeyData;
        return null;
    }

    public double GetAudioDecibel(string audioPresetId, string audioId) =>
        GetAudioBinding(audioPresetId, audioId, create: false)?.Decibel ?? 0;

    public void SetAudioDecibel(string audioPresetId, string audioId, double decibel)
    {
        var binding = GetAudioBinding(audioPresetId, audioId, create: true);
        binding.Decibel = decibel;
        if (decibel == 0 && binding.HotkeyData.VkCode == 0)
            Bindings.Remove(binding);
    }

    public bool ClearHotkey(string audioPresetId, string audioId)
    {
        var binding = GetAudioBinding(audioPresetId, audioId, create: false);
        if (binding == null)
            return false;

        binding.HotkeyData.Clear();
        if (binding.Decibel == 0)
            Bindings.Remove(binding);
        return true;
    }

    public bool RemoveAudioBinding(string audioPresetId, string audioId) =>
        Bindings.RemoveAll(item =>
            item.AudioPresetId == audioPresetId && item.AudioId == audioId) > 0;

    public bool RemoveAudioPresetBindings(string audioPresetId) =>
        Bindings.RemoveAll(item => item.AudioPresetId == audioPresetId) > 0;

    public bool Save() => FunctionPresetDataTool.Save(this);

    private FunctionPresetAudioBinding GetAudioBinding(
        string audioPresetId, string audioId, bool create)
    {
        var binding = Bindings.FirstOrDefault(item =>
            item.AudioPresetId == audioPresetId && item.AudioId == audioId);
        if (binding != null || !create)
            return binding;

        binding = new FunctionPresetAudioBinding
        {
            AudioPresetId = audioPresetId,
            AudioId = audioId,
        };
        Bindings.Add(binding);
        return binding;
    }
}

public class FunctionPresetAudioBinding
{
    public string AudioPresetId { get; set; }
    public string AudioId { get; set; }
    public HotkeyData HotkeyData { get; set; } = new();
    public double Decibel { get; set; }
}
