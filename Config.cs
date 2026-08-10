using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Themes;
using System.IO;
using System.Text.Json.Serialization;

namespace PlayVoice;

internal class Config
{
    public ThemeManager.ThemeEnum Theme { get; set; } = ThemeManager.ThemeEnum.System;
    public string Language { get; set; } = "zh-CN";

    public bool AutoMute { get; set; } = true;

    public bool MinimizeToTrayOnClose { get; set; } = false;
    public bool HasSelectedCloseBehavior { get; set; } = false;
    public bool HasAcknowledgedTutorialPrompt { get; set; } = false;

    public double AudioOutDecibel { get; set; } = 0;
    public double AudioEarDecibel { get; set; } = 0;
    public double MicrophoneInputDecibel { get; set; } = 0;
    public double? MicrophoneLufs { get; set; }
    public double GlobalDecibel { get; set; } = 0;

    public bool AcceptedUserGeneratedContentAgreement { get; set; } = false;

    public string PhysicalMicrophoneID { get; set; }
    public string PhysicalLoudspeakerID { get; set; }
    public string VirtualMicrophoneID { get; set; }
    public string VirtualLoudspeakerID { get; set; }

    public string ActiveAudioPresetName { get; set; }
    public string ActiveFunctionPresetName { get; set; }

    public bool Save()
    {
        return JsonTool.SaveJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), this);
    }
}
