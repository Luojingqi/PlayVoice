using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.HeaderToolTip;

public partial class HeaderToolTip : UserControl
{
    public HeaderToolTip()
    {
        InitializeComponent();
        SetResourceReference(HeaderFontSizeProperty, "UiFontSize16");
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(HeaderToolTip),
            new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty ToolTipTextProperty =
        DependencyProperty.Register(
            nameof(ToolTipText),
            typeof(string),
            typeof(HeaderToolTip),
            new PropertyMetadata(null));

    public string ToolTipText
    {
        get => (string)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    public static readonly DependencyProperty HeaderFontSizeProperty =
        DependencyProperty.Register(
            nameof(HeaderFontSize),
            typeof(double),
            typeof(HeaderToolTip),
            new PropertyMetadata(16.0));

    public double HeaderFontSize
    {
        get => (double)GetValue(HeaderFontSizeProperty);
        set => SetValue(HeaderFontSizeProperty, value);
    }

    private void HeaderPanel_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ToolTipText))
            e.Handled = true;
    }
}
