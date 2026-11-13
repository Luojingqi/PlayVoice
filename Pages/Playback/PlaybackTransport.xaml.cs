using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.Playback;

public partial class PlaybackTransport : UserControl
{
    public PlaybackTransport()
    {
        InitializeComponent();
        Loaded += (_, _) => { GlobalData.Inst.Playback.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => GlobalData.Inst.Playback.Changed -= Refresh;
    }
    private void Refresh()
    {
        PauseIcon.Data = System.Windows.Media.Geometry.Parse(GlobalData.Inst.Playback.IsPlaying ? "M 4,3 H 8 V 17 H 4 Z M 12,3 H 16 V 17 H 12 Z" : "M 6,3 L 17,10 L 6,17 Z");
        Buttons.IsEnabled = GlobalData.Inst.ActiveAudioPreset?.AudioList.Count > 0;
    }
    private void Control_Click(object sender, RoutedEventArgs e)
    {
        var playback = GlobalData.Inst.Playback;
        switch ((sender as Button)?.Tag as string)
        {
            case "Previous": playback.Previous(); break;
            case "Play": playback.PauseOrResume(); break;
            case "Next": playback.Next(); break;
        }
    }
}
