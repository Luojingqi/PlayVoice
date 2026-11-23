using PlayVoice.Hotkey;
using System.Windows;
using PlayVoice.Audio;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PlayVoice.Pages.Playback;
using System.Windows.Media.Imaging;
using PlayVoice.Pages;

namespace PlayVoice.Pages.Preset;

public partial class AudioTrackItem : UserControl
{
    public AudioTrackItemViewModel ViewModel => (AudioTrackItemViewModel)this.DataContext;
    private AudioData subscribedAudioData;

    public AudioTrackItem()
    {
        InitializeComponent();
        CheckBoxListBox.SelectionChanged += CheckBoxListBox_SelectionChanged;
        Loaded += AudioTrackItem_Loaded;
        Unloaded += AudioTrackItem_Unloaded;
        DataContextChanged += AudioTrackItem_DataContextChanged;
    }

    private void CheckBoxListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CheckBoxListBox.SelectedIndex == -1) return;
        ViewModel.Marked = !ViewModel.Marked;
        CheckBoxListBox.SelectedIndex = -1;
    }

    private void RowBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as DependencyObject;

        if (IsChildOf(CheckBoxListBox, originalSource)) return;
        if (IsChildOf(VolumeSlider, originalSource)) return;
        if (IsChildOf(DragHandle, originalSource)) return;
        if (IsChildOf(KeyboardKeyListBox, originalSource)) return;
        if (IsChildOf(RowPlayButton, originalSource)) return;

        ClickRippleEffect.Start(RippleCanvas, e.GetPosition(RippleCanvas));
        var container = FindVisualParent<ListBoxItem>(this);
        if (container != null) container.IsSelected = true;
        if (e.ClickCount == 2) GlobalData.Inst.Playback.Toggle(ViewModel.Data);
    }

    private void RowPlay_Click(object sender, RoutedEventArgs e) => GlobalData.Inst.Playback.Toggle(ViewModel.Data);

    private void RippleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClickRippleEffect.UpdateClip(RippleCanvas, e.NewSize);
    }

    private void AudioTrackItem_Loaded(object sender, RoutedEventArgs e)
    {
        SubscribeToPlaybackState();
        SynchronizePlaybackAnimation();
    }

    private void AudioTrackItem_Unloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeFromPlaybackState();
        SynchronizePlaybackAnimation();
    }

    private void AudioTrackItem_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (KeyboardKeyInputTextBox.IsKeyboardFocusWithin)
            Keyboard.ClearFocus();
        if (!IsLoaded) return;

        SubscribeToPlaybackState();
        SynchronizePlaybackAnimation();
    }

    private void SubscribeToPlaybackState()
    {
        var audioData = (DataContext as AudioTrackItemViewModel)?.Data;
        if (ReferenceEquals(subscribedAudioData, audioData)) return;

        UnsubscribeFromPlaybackState();
        subscribedAudioData = audioData;
        if (subscribedAudioData == null) return;

        subscribedAudioData.PlaybackChanged += SynchronizePlaybackAnimation;
    }

    private void UnsubscribeFromPlaybackState()
    {
        if (subscribedAudioData == null) return;

        subscribedAudioData.PlaybackChanged -= SynchronizePlaybackAnimation;
        subscribedAudioData = null;
    }

    private void SynchronizePlaybackAnimation()
    {
        var snapshot = subscribedAudioData?.Snapshot ?? default;
        RowPlayIcon.Text = snapshot.State == TrackPlaybackState.Playing ? "\uE769" : "\uE768";
        PlaybackAnimation.Synchronize(BackgroundScale, snapshot);
    }

    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var dragged = ViewModel;
        var dragData = new DataObject(typeof(AudioTrackItemViewModel), dragged);
        var dpi = VisualTreeHelper.GetDpi(this);
        int width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        int height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        if (width > 0 && height > 0)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bmp.Render(this);
            dragData.SetData("AudioTrackDragImage", bmp);
        }

        ScrollViewer scrollViewer = FindVisualParent<ScrollViewer>(this);
        bool isDragging = true;

        void ScrollDuringDrag(int delta, int screenX, int screenY)
        {
            if (!isDragging || scrollViewer == null || delta == 0)
                return;

            void RaiseMouseWheelEvent()
            {
                if (!isDragging || !scrollViewer.IsLoaded)
                    return;

                Point position = scrollViewer.PointFromScreen(new Point(screenX, screenY));
                if (!new Rect(scrollViewer.RenderSize).Contains(position))
                    return;

                var wheelEvent = new MouseWheelEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    delta)
                {
                    RoutedEvent = Mouse.MouseWheelEvent,
                    Source = scrollViewer
                };
                scrollViewer.RaiseEvent(wheelEvent);
            }

            if (scrollViewer.Dispatcher.CheckAccess())
                RaiseMouseWheelEvent();
            else
                scrollViewer.Dispatcher.BeginInvoke(RaiseMouseWheelEvent);
        }

        HotkeyManager.Inst.MouseWheelScrolled += ScrollDuringDrag;
        dragged.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop(this, dragData, DragDropEffects.Move);
        }
        finally
        {
            isDragging = false;
            HotkeyManager.Inst.MouseWheelScrolled -= ScrollDuringDrag;
            dragged.IsDragging = false;
        }

        e.Handled = true;
    }

    private static T FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        DependencyObject current = child;
        while (current != null)
        {
            if (current is T parent)
                return parent;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private bool IsChildOf(DependencyObject parent, DependencyObject child)
    {
        if (child == null) return false;
        if (child == parent) return true;
        return IsChildOf(parent, VisualTreeHelper.GetParent(child));
    }

    private void KeyboardKeyInputTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        HotkeyManager.Inst.StartRecording(OnHotkeyRecorded);
    }

    private void KeyboardKeyInputTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        HotkeyManager.Inst.StopRecording();
    }

    private void OnHotkeyRecorded(HotkeyData newHotkey)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (newHotkey == null)
            {
                ClearHotkey();
            }
            else
            {
                var viewModel = ViewModel;
                var hotkeyData = GlobalData.Inst.GetAudioHotkey(viewModel.Data, create: true);
                if (hotkeyData == null)
                {
                    Keyboard.Focus(null);
                    return;
                }
                if (HotkeyManager.Inst.IsAssigned(newHotkey, hotkeyData))
                {
                    MainWindow.Inst.AddNotification(
                        () => PlayVoice.Resources.Language.LanguageManager.Inst.GetString("通知"),
                        () => PlayVoice.Resources.Language.LanguageManager.Inst.GetString("快捷键已占用"),
                        LabelStatus.Warning, 4);
                    Keyboard.Focus(null);
                    return;
                }
                hotkeyData.Modifiers = newHotkey.Modifiers;
                hotkeyData.VkCode = newHotkey.VkCode;
                hotkeyData.IsMouse = newHotkey.IsMouse;

                viewModel.RefreshHotkey();
                KeyboardKeyInputTextBox.SelectionStart = KeyboardKeyInputTextBox.Text.Length;
            }
            Keyboard.Focus(null);
            GlobalData.Inst.SaveActiveFunctionPreset();
            GlobalData.Inst.RebuildActiveHotkeys();
        });
    }

    private void ClearHotkey()
    {
        var audioData = ViewModel.Data;
        GlobalData.Inst.ActiveFunctionPreset?.ClearHotkey(
            audioData.AudioPreset.Config.Name, audioData.Config.Id);
        ViewModel.RefreshHotkey();
        KeyboardKeyInputTextBox.SelectionStart = KeyboardKeyInputTextBox.Text.Length;
    }
}
