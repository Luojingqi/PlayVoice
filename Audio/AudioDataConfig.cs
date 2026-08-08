using PlayVoice.Hotkey;
using System.Text.Json.Serialization;

namespace PlayVoice.Audio;

public class AudioDataConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; }
    public string FileFormat { get; set; }
    public long Size { get; set; }
    public double? Lufs { get; set; }
    public double Decibel { get; set; } = 0;
    [JsonIgnore]
    public string Name => FileName + FileFormat;
}
