using PlayVoice.Hotkey;
using PlayVoice.Pages.FunctionPreset;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.Playback;

public partial class PlaybackSettings : UserControl
{
    private TextBox recordingInput;
    public PlaybackSettings()
    {
        InitializeComponent();
        Loaded += (_, _) => { GlobalData.Inst.ActiveFunctionPresetChanged += PresetChanged; Refresh(); };
        Unloaded += (_, _) => { GlobalData.Inst.ActiveFunctionPresetChanged -= PresetChanged; StopRecording(); };
    }
    private void PresetChanged(FunctionPresetData preset) { StopRecording(); Keyboard.Focus(null); Refresh(); }
    private HotkeyData GetHotkey(TextBox input)
    {
        var preset = GlobalData.Inst.ActiveFunctionPreset;
        if (preset == null) return null;
        if (input == PauseInput) return preset.PausePlaybackHotkey;
        if (input == PreviousInput) return preset.PreviousTrackHotkey;
        if (input == StopInput) return preset.StopAllHotkey ??= new();
        return preset.NextTrackHotkey;
    }
    private void Refresh()
    {
        IsEnabled = GlobalData.Inst.ActiveFunctionPreset != null;
        foreach (var input in new[] { PreviousInput, PauseInput, NextInput, StopInput })
            input.Text = GetHotkey(input)?.ToString() ?? "None";
    }
    private void Hotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        recordingInput = (TextBox)sender;
        var preset = GlobalData.Inst.ActiveFunctionPreset;
        var hotkey = GetHotkey(recordingInput);
        HotkeyManager.Inst.StartRecording(value => Dispatcher.Invoke(() =>
        {
            if (hotkey == null || preset != GlobalData.Inst.ActiveFunctionPreset) return;
            if (HotkeyManager.Inst.IsAssigned(value, hotkey))
            {
                MainWindow.Inst.AddNotification(
                    () => PlayVoice.Resources.Language.LanguageManager.Inst.GetString("通知"),
                    () => PlayVoice.Resources.Language.LanguageManager.Inst.GetString("快捷键已占用"), LabelStatus.Warning, 4);
                Keyboard.Focus(null);
                return;
            }
            if (value == null) hotkey.Clear();
            else { hotkey.Modifiers = value.Modifiers; hotkey.VkCode = value.VkCode; hotkey.IsMouse = value.IsMouse; }
            Refresh();
            Keyboard.Focus(null);
            GlobalData.Inst.SaveActiveFunctionPreset();
            GlobalData.Inst.RebuildActiveHotkeys();
        }));
    }
    private void StopRecording()
    {
        if (recordingInput == null) return;
        HotkeyManager.Inst.StopRecording();
        recordingInput = null;
    }
    private void Hotkey_LostFocus(object sender, RoutedEventArgs e) => StopRecording();
    private void Controls_ExpandedChanged(object sender, RoutedPropertyChangedEventArgs<bool> e) =>
        AnimatePanel(ControlHotkeyPanel, e.NewValue, 4 * 40.5);

    private static void AnimatePanel(FrameworkElement panel, bool expanded, double height)
    {
        if (!expanded && panel.IsKeyboardFocusWithin) Keyboard.Focus(null);
        panel.IsHitTestVisible = expanded;
        panel.BeginAnimation(HeightProperty, Animation(panel.Height, expanded ? height : 0));
        panel.BeginAnimation(OpacityProperty, Animation(panel.Opacity, expanded ? 1 : 0));
        if (panel.RenderTransform is TranslateTransform transform)
            transform.BeginAnimation(TranslateTransform.YProperty, Animation(transform.Y, expanded ? 0 : -6));
    }

    private static DoubleAnimation Animation(double from, double to) => new(from, to,
        TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
}
