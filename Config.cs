using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Themes;
using System.IO;

namespace PlayVoice;

internal class Config
{
    public ThemeManager.ThemeEnum Theme { get; set; } = ThemeManager.ThemeEnum.Dark;
    public string Language { get; set; } = "zh-CN";

    public bool AutoMute { get; set; } = false;

    public bool MinimizeToTrayOnClose { get; set; } = false;

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

    public PlayAudioKeyData BeforePlayingKey { get; set; } = new() { Action = PlayAudioKeyData.KeyAction.按下 };

    public PlayAudioKeyData AfterPlayingKey { get; set; } = new() { Action = PlayAudioKeyData.KeyAction.抬起 };

    public bool Save()
    {
        return JsonTool.SaveJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), this);
    }
}
