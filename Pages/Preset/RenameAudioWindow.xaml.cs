using PlayVoice.Audio;
using PlayVoice.Diagnostics;
using PlayVoice.Resources.Language;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlayVoice.Pages.Preset;

public partial class RenameAudioWindow : Window
{
    private readonly AudioData audio;
    public RenameAudioWindow(AudioData audio)
    {
        InitializeComponent();
        this.audio = audio;
        OriginalName.Text = audio.Config.Name;
        NewName.Text = audio.Config.FileName;
        Loaded += (_, _) => { NewName.Focus(); NewName.SelectAll(); };
    }
    private void Confirm()
    {
        try { audio.AudioPreset.RenameAudio(audio, NewName.Text); DialogResult = true; }
        catch (Exception exception)
        {
            AppLog.Warning(exception, "重命名音频失败。", "AudioPreset.Rename");
            ErrorText.Text = LanguageManager.Inst.GetString("重命名失败") + " " + exception.Message;
        }
    }
    private void Confirm_Click(object sender, SelectionChangedEventArgs e)
    {
        var list = (ListBox)sender;
        if (list.SelectedIndex < 0) return;
        list.SelectedIndex = -1;
        Confirm();
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.Enter) { Confirm(); e.Handled = true; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Title_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
