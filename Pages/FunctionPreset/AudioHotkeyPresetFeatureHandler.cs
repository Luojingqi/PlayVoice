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
                    audio.Start();
            };
            appliedHotkeys.Add(hotkey);
            HotkeyManager.Inst.AddHotkey(hotkey);
        }
    }

    public void Clear()
    {
        HotkeyManager.Inst.ClearHotkeys();
        foreach (var hotkey in appliedHotkeys)
            hotkey.Callback = null;
        appliedHotkeys.Clear();
    }
}
