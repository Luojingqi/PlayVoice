using PlayVoice.Hotkey;
using PlayVoice.Resources.Themes;

namespace PlayVoice.ConfigVersion;

/// <summary>
/// Config v8 的属性快照。
/// </summary>
internal class config_v8
{
    public ThemeManager.ThemeEnum Theme { get; set; } = ThemeManager.ThemeEnum.System;

    public string Language { get; set; } = "en-US";

    public bool AutoMute { get; set; } = true;

    public HotkeyData EarAudioToggleHotkey { get; set; } = new();

    public HotkeyData EarInToggleHotkey { get; set; } = new();

    public HotkeyData AutoMuteToggleHotkey { get; set; } = new();

    public int MaxPlayingAudioCount { get; set; } = 2;

    public bool MinimizeToTrayOnClose { get; set; } = false;

    public bool HasSelectedCloseBehavior { get; set; } = false;

    public bool HasAcknowledgedTutorialPrompt { get; set; } = false;

    public double AudioOutDecibel { get; set; } = 0;

    public double AudioEarDecibel { get; set; } = 0;

    public double MicrophoneInputDecibel { get; set; } = 0;

    public double? MicrophoneLufs { get; set; }

    public string MicrophoneLufsDeviceID { get; set; }

    public Dictionary<string, double> MicrophoneLufsByDeviceID { get; set; } =
        new(StringComparer.Ordinal);

    public double GlobalDecibel { get; set; } = 0;

    public bool AcceptedUserGeneratedContentAgreement { get; set; } = false;

    public string PhysicalMicrophoneID { get; set; }

    public string PhysicalLoudspeakerID { get; set; }

    public string VirtualMicrophoneID { get; set; }

    public string VirtualLoudspeakerID { get; set; }

    public string ActiveAudioPresetName { get; set; }

    public string ActiveFunctionPresetName { get; set; }

    public bool HasDetectedInitialSteamLanguage { get; set; } = false;
}
