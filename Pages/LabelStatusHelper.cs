using System.Windows;

namespace PlayVoice.Pages;

public static class LabelStatusHelper
{
    public static readonly DependencyProperty LabelStatusProperty =
        DependencyProperty.RegisterAttached(
            "LabelStatus",
            typeof(LabelStatus),
            typeof(LabelStatusHelper),
            new PropertyMetadata(LabelStatus.None));

    public static LabelStatus GetLabelStatus(DependencyObject obj) =>
        (LabelStatus)obj.GetValue(LabelStatusProperty);

    public static void SetLabelStatus(DependencyObject obj, LabelStatus value) =>
        obj.SetValue(LabelStatusProperty, value);
}
