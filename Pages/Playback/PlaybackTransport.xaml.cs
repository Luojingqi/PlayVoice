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
        PauseIcon.Text = GlobalData.Inst.Playback.IsPlaying ? "\uE769" : "\uE768";
        Buttons.IsEnabled = GlobalData.Inst.ActiveAudioPreset?.AudioList.Count > 0;
    }
    private void Control_Click(object sender, SelectionChangedEventArgs e)
    {
        int index = Buttons.SelectedIndex;
        if (index < 0) return;
        Buttons.SelectedIndex = -1;
        var playback = GlobalData.Inst.Playback;
        switch (index)
        {
            case 0: playback.Previous(); break;
            case 1: playback.PauseOrResume(); break;
            case 2: playback.Next(); break;
        }
    }
}
