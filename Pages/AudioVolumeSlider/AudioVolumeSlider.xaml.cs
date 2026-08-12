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

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            "Minimum",
            typeof(double),
            typeof(AudioVolumeSlider),
            new PropertyMetadata(0.0));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            "Maximum",
            typeof(double),
            typeof(AudioVolumeSlider),
            new PropertyMetadata(200.0));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty TickFrequencyProperty =
        DependencyProperty.Register(
            "TickFrequency",
            typeof(double),
            typeof(AudioVolumeSlider),
            new PropertyMetadata(1.0));

    public double TickFrequency
    {
        get => (double)GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
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
        if (e.Delta == 0) return;

        double change = e.Delta > 0 ? TickFrequency : -TickFrequency;
        Value = Math.Clamp(Value + change, Minimum, Maximum);
    }
}
