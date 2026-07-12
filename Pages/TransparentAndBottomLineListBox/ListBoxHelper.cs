using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.TransparentAndBottomLineListBox;
public static class ListBoxHelper
{
    public enum LinePosition
    {
        Bottom,
        Top
    }

    public static LinePosition GetLinePosition(DependencyObject obj)
        => (LinePosition)obj.GetValue(LinePositionProperty);

    public static void SetLinePosition(DependencyObject obj, LinePosition value)
        => obj.SetValue(LinePositionProperty, value);

    public static readonly DependencyProperty LinePositionProperty =
        DependencyProperty.RegisterAttached(
            "LinePosition",
            typeof(LinePosition),
            typeof(ListBoxHelper),
            new PropertyMetadata(LinePosition.Bottom));
}