using PlayVoice.Audio;

namespace PlayVoice.Pages.Preset;

public class AudioPresetDataConfig
{
    public int Version { get; set; } = 1;
    public string Name { get; set; }
    public List<AudioDataConfig> AudioDataConfigList { get; set; } = new();
}
