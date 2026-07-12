using PlayVoice.Hotkey;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PlayVoice.Pages.Preset;

public partial class AudioTrackItem : UserControl
{
    public AudioTrackItemViewModel ViewModel => (AudioTrackItemViewModel)this.DataContext;

    public AudioTrackItem()
    {
        InitializeComponent();
        CheckBoxListBox.SelectionChanged += CheckBoxListBox_SelectionChanged;
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

        ViewModel.Data.Start();
    }

    public void PlayClickAnimation()
    {
        DoubleAnimation scaleAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(((AudioTrackItemViewModel)this.DataContext).Duration)
        };

        BackgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
    }

    public void ResetAnimation()
    {
        BackgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        BackgroundScale.ScaleX = 0;
    }

    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        this.ResetAnimation();

        int width = (int)this.ActualWidth;
        int height = (int)this.ActualHeight;
        if (width > 0 && height > 0)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(this);
            this.Tag = bmp;
        }

        this.Visibility = Visibility.Hidden;

        DataObject dragData = new DataObject("AudioTrackItemVisual", this);
        DragDrop.DoDragDrop(this, dragData, DragDropEffects.Move);

        this.Visibility = Visibility.Visible;
        this.Tag = null;

        e.Handled = true;
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
                var hotkeyData = viewModel.Data.Config.HotkeyData;
                hotkeyData.Modifiers = newHotkey.Modifiers;
                hotkeyData.VkCode = newHotkey.VkCode;
                hotkeyData.IsMouse = newHotkey.IsMouse;

                viewModel.Hotkey = hotkeyData.ToString();
                KeyboardKeyInputTextBox.SelectionStart = KeyboardKeyInputTextBox.Text.Length;
            }
            Keyboard.Focus(null);
            GlobalData.Inst.PresetData.Save();
        });
    }

    private void ClearHotkey()
    {
        var hotkeyData = ViewModel.Data.Config.HotkeyData;
        hotkeyData.Clear();

        ViewModel.Hotkey = hotkeyData.ToString();
        KeyboardKeyInputTextBox.SelectionStart = KeyboardKeyInputTextBox.Text.Length;
    }
}