using PlayVoice.Hotkey;
using System.Text.Json.Serialization;

namespace PlayVoice.Audio;

public class AudioDataConfig
{
    public string FileName { get; set; }
    public string FileFormat { get; set; }
    public long Size { get; set; }
    public double? Lufs { get; set; }
    public double Decibel { get; set; } = 0;
    public HotkeyData HotkeyData {  get; set; }

    [JsonIgnore]
    public string Name => FileName + FileFormat;
}
