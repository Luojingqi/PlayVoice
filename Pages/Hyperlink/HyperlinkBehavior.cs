using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.Hyperlink;

internal class HyperlinkBehavior
{
    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.RegisterAttached("Url", typeof(string), typeof(HyperlinkBehavior),
            new PropertyMetadata(null, OnUrlChanged));

    public static string GetUrl(DependencyObject obj) => (string)obj.GetValue(UrlProperty);
    public static void SetUrl(DependencyObject obj, string value) => obj.SetValue(UrlProperty, value);

    private static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Button button)
        {
            button.Click -= OnButtonClick;
            if (e.NewValue != null)
            {
                button.Click += OnButtonClick;
            }
        }
    }

    private static void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && GetUrl(button) is string url && !string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
