using PlayVoice.Hotkey;
using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Language;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayVoice.Pages.FunctionPreset;

public class FunctionPresetData
{
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

    public HotkeyData GetHotkey(string audioPresetName, string audioId, bool create)
    {
        var binding = GetAudioBinding(audioPresetName, audioId, create);
        if (binding != null)
            return binding.HotkeyData;
        return null;
    }

    public double GetAudioDecibel(string audioPresetName, string audioId) =>
        GetAudioBinding(audioPresetName, audioId, create: false)?.Decibel ?? 0;

    public void SetAudioDecibel(string audioPresetName, string audioId, double decibel)
    {
        var binding = GetAudioBinding(audioPresetName, audioId, create: true);
        binding.Decibel = decibel;
        if (decibel == 0 && binding.HotkeyData.VkCode == 0)
            Bindings.Remove(binding);
    }

    public bool ClearHotkey(string audioPresetName, string audioId)
    {
        var binding = GetAudioBinding(audioPresetName, audioId, create: false);
        if (binding == null)
            return false;

        binding.HotkeyData.Clear();
        if (binding.Decibel == 0)
            Bindings.Remove(binding);
        return true;
    }

    public bool RemoveAudioBinding(string audioPresetName, string audioId) =>
        Bindings.RemoveAll(item =>
            item.AudioPresetName == audioPresetName && item.AudioId == audioId) > 0;

    public bool RemoveAudioPresetBindings(string audioPresetName) =>
        Bindings.RemoveAll(item => item.AudioPresetName == audioPresetName) > 0;

    public bool RenameAudioPresetBindings(string oldName, string newName)
    {
        bool changed = false;
        foreach (var binding in Bindings.Where(item =>
            string.Equals(
                item.AudioPresetName, oldName, StringComparison.OrdinalIgnoreCase)))
        {
            binding.AudioPresetName = newName;
            changed = true;
        }
        return changed;
    }

    public bool Save() => FunctionPresetDataTool.Save(this);

    private FunctionPresetAudioBinding GetAudioBinding(
        string audioPresetName, string audioId, bool create)
    {
        var binding = Bindings.FirstOrDefault(item =>
            item.AudioPresetName == audioPresetName && item.AudioId == audioId);
        if (binding != null || !create)
            return binding;

        binding = new FunctionPresetAudioBinding
        {
            AudioPresetName = audioPresetName,
            AudioId = audioId,
        };
        Bindings.Add(binding);
        return binding;
    }
}

public class FunctionPresetAudioBinding
{
    public string AudioPresetName { get; set; }
    public string AudioId { get; set; }
    public HotkeyData HotkeyData { get; set; } = new();
    public double Decibel { get; set; }
}
