using PlayVoice.Hotkey;

namespace PlayVoice.Pages.Workshop;

public class PlayAudioKeyData
{
    public enum KeyAction
    {
        按下,
        抬起,
        单击,
    }

    public KeyAction Action { get; set; }

    public HotkeyData HotkeyData { get; set; } = new();
}
