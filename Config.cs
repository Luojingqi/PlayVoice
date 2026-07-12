using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Themes;
using System.IO;

namespace PlayVoice;

internal class Config
{
    public ThemeManager.ThemeEnum? Theme { get; set; }
    public string Language { get; set; }

    public bool AutoMute { get; set; } = false;

    public double AudioDecibel { get; set; } = 0;
    public double MicrophoneInputDecibel { get; set; } = 1.2;
    public double GlobalDecibel { get; set; } = 0;

    public string PhysicalMicrophoneID { get; set; }
    public string PhysicalLoudspeakerID { get; set; }
    public string VirtualMicrophoneID { get; set; }
    public string VirtualLoudspeakerID { get; set; }

    public PlayAudioKeyData BeforePlayingKey { get; set; } = new();

    public PlayAudioKeyData AfterPlayingKey { get; set; } = new();

    public bool Save()
    {
        return JsonTool.SaveJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), this);
    }
}
