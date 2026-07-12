using NAudio.Gui;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlayVoice.Pages.AudioVolumeSlider;

/// <summary>
/// AudioVolumeSlider.xaml 的交互逻辑
/// </summary>
public partial class AudioVolumeSlider : UserControl
{
    public AudioVolumeSlider()
    {
        InitializeComponent();
        this.PreviewMouseWheel += VolumeSlider_PreviewMouseWheel;
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            "Value",
            typeof(double),
            typeof(AudioVolumeSlider),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly RoutedEvent ValueChangedEvent =
        EventManager.RegisterRoutedEvent(
            "ValueChanged",
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<double>),
            typeof(VolumeSlider));

    public event RoutedPropertyChangedEventHandler<double> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    // 当属性改变时触发路由事件
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioVolumeSlider control)
        {
            var args = new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue, ValueChangedEvent);
            control.RaiseEvent(args);
        }
    }

    private void VolumeSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (e.Delta > 0)
        {
            if (Value < InnerSlider.Maximum) Value += 1;
        }
        else if (e.Delta < 0)
        {
            if (Value > InnerSlider.Minimum) Value -= 1;
        }
    }
}
