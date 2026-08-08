using PlayVoice.Audio;

namespace PlayVoice.Pages.Preset;

public class AudioPresetDataConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public List<AudioDataConfig> AudioDataConfigList { get; set; } = new();
}
