using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.ExpandArrow;

/// <summary>
/// ExpandArrow.xaml 的交互逻辑
/// </summary>
public partial class ExpandArrow : UserControl
{
    public ExpandArrow()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(ExpandArrow),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly RoutedEvent ExpandedChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ExpandedChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<bool>),
            typeof(ExpandArrow));

    public event RoutedPropertyChangedEventHandler<bool> ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExpandArrow control)
        {
            var args = new RoutedPropertyChangedEventArgs<bool>(
                (bool)e.OldValue,
                (bool)e.NewValue,
                ExpandedChangedEvent);
            control.RaiseEvent(args);
        }
    }
}
