using PlayVoice.Hotkey;
using System.Windows;
using PlayVoice.Audio;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ViewModel.MarkedChanged += b =>
        {
            if (b)
                SelectCenterPoint.Visibility = Visibility.Visible;
            else
                SelectCenterPoint.Visibility = Visibility.Collapsed;
        };
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

        ClickRippleEffect.Start(RippleCanvas, e.GetPosition(RippleCanvas));
        ViewModel.Data.Start();
    }

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
        ResetPlaybackAnimation();
    }

    private void AudioTrackItem_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
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

        subscribedAudioData.PlaybackStarted += AudioData_PlaybackStarted;
        subscribedAudioData.PlaybackStopped += AudioData_PlaybackStopped;
    }

    private void UnsubscribeFromPlaybackState()
    {
        if (subscribedAudioData == null) return;

        subscribedAudioData.PlaybackStarted -= AudioData_PlaybackStarted;
        subscribedAudioData.PlaybackStopped -= AudioData_PlaybackStopped;
        subscribedAudioData = null;
    }

    private void AudioData_PlaybackStarted()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(SynchronizePlaybackAnimation);
            return;
        }

        SynchronizePlaybackAnimation();
    }

    private void AudioData_PlaybackStopped()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ResetPlaybackAnimation);
            return;
        }

        ResetPlaybackAnimation();
    }

    private void SynchronizePlaybackAnimation()
    {
        if (!IsLoaded || DataContext is not AudioTrackItemViewModel viewModel)
            return;

        var audioData = viewModel.Data;
        double progress = audioData.PlaybackProgress;
        if (!audioData.IsPlaying || progress >= 1)
        {
            ResetPlaybackAnimation();
            return;
        }

        double remainingMilliseconds =
            viewModel.Duration.TotalMilliseconds * (1 - progress);
        var animation = new DoubleAnimation
        {
            From = progress,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(remainingMilliseconds),
            FillBehavior = FillBehavior.HoldEnd
        };
        BackgroundScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void ResetPlaybackAnimation()
    {
        BackgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        BackgroundScale.ScaleX = 0;
    }

    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        int width = (int)this.ActualWidth;
        int height = (int)this.ActualHeight;
        if (width > 0 && height > 0)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(this);
            this.Tag = bmp;
        }

        DataObject dragData = new DataObject("AudioTrackItemVisual", this);
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
        this.Visibility = Visibility.Hidden;
        try
        {
            DragDrop.DoDragDrop(this, dragData, DragDropEffects.Move);
        }
        finally
        {
            isDragging = false;
            HotkeyManager.Inst.MouseWheelScrolled -= ScrollDuringDrag;
            this.Visibility = Visibility.Visible;
            this.Tag = null;
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
