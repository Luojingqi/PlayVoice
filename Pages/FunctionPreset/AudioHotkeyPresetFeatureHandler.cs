using PlayVoice.Hotkey;
using PlayVoice.Pages.Preset;

namespace PlayVoice.Pages.FunctionPreset;

public sealed class AudioHotkeyPresetFeatureHandler : IFunctionPresetFeatureHandler
{
    private readonly List<HotkeyData> appliedHotkeys = new();

    public string FeatureKey => "AudioHotkeys";

    public void Apply(FunctionPresetData functionPreset, AudioPresetData audioPreset)
    {
        if (functionPreset == null || audioPreset == null)
            return;

        foreach (var audio in audioPreset.AudioList)
        {
            var hotkey = functionPreset.GetHotkey(
                audioPreset.Config.Name, audio.Config.Id, create: false);
            if (hotkey == null || hotkey.VkCode == 0)
                continue;

            hotkey.Callback = () =>
            {
                if (GlobalData.Inst.GetRun())
                    GlobalData.Inst.Playback.Toggle(audio);
            };
            appliedHotkeys.Add(hotkey);
            HotkeyManager.Inst.AddHotkey(hotkey);
        }
    }

    public void Clear()
    {
        foreach (var hotkey in appliedHotkeys)
        {
            HotkeyManager.Inst.RemoveHotkey(hotkey);
            hotkey.Callback = null;
        }
        appliedHotkeys.Clear();
    }
}
