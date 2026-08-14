using PlayVoice.Hotkey;
using PlayVoice.Pages.Preset;

namespace PlayVoice.Pages.FunctionPreset;

public sealed class StopAllPlayingPresetFeatureHandler : IFunctionPresetFeatureHandler
{
    private HotkeyData appliedHotkey;

    public string FeatureKey => "StopAllPlaying";

    public void Apply(FunctionPresetData functionPreset, AudioPresetData audioPreset)
    {
        var hotkey = functionPreset?.StopAllPlayingHotkey;
        if (hotkey == null || hotkey.VkCode == 0)
            return;

        hotkey.Callback = () => GlobalData.Inst.AudioProxy.StopClear();
        appliedHotkey = hotkey;
        HotkeyManager.Inst.AddHotkey(hotkey);
    }

    public void Clear()
    {
        if (appliedHotkey == null)
            return;

        HotkeyManager.Inst.RemoveHotkey(appliedHotkey);
        appliedHotkey.Callback = null;
        appliedHotkey = null;
    }
}
