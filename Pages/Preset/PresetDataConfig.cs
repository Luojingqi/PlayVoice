using PlayVoice.Audio;

namespace PlayVoice.Pages.Preset;

public class PresetDataConfig
{
    public string Name { get; set; }
    public List<AudioDataConfig> AudioDataConfigList { get; set; } = new();
}
