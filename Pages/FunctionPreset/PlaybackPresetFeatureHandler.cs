using PlayVoice.Hotkey;
using PlayVoice.Pages.Preset;

namespace PlayVoice.Pages.FunctionPreset;

public sealed class PlaybackPresetFeatureHandler : IFunctionPresetFeatureHandler
{
    private readonly List<HotkeyData> applied = new();
    public string FeatureKey => "PlaybackControls";
    public void Apply(FunctionPresetData functionPreset, AudioPresetData audioPreset)
    {
        if (functionPreset == null) return;
        var playback = GlobalData.Inst.Playback;
        Bind(functionPreset.PausePlaybackHotkey, playback.PauseOrResume);
        Bind(functionPreset.PreviousTrackHotkey, playback.Previous);
        Bind(functionPreset.NextTrackHotkey, playback.Next);
    }
    private void Bind(HotkeyData hotkey, Action action)
    {
        if (hotkey == null || hotkey.VkCode == 0) return;
        hotkey.Callback = action;
        applied.Add(hotkey);
        HotkeyManager.Inst.AddHotkey(hotkey);
    }
    public void Clear()
    {
        foreach (var hotkey in applied)
        {
            HotkeyManager.Inst.RemoveHotkey(hotkey);
            hotkey.Callback = null;
        }
        applied.Clear();
    }
}
