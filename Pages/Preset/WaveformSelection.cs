using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PlayVoice.Pages.Preset;

public sealed class WaveformSelection : Control
{
    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(nameof(Peaks), typeof(float[]), typeof(WaveformSelection), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(nameof(Start), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public float[] Peaks { get => (float[])GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public double Start { get => (double)GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public double End { get => (double)GetValue(EndProperty); set => SetValue(EndProperty, value); }
    private double anchor;
    private int handle;

    public WaveformSelection()
    {
        Focusable = true;
        MinHeight = 112;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        SetResourceReference(ForegroundProperty, "AccentColor");
        SetResourceReference(BackgroundProperty, "BgDark");
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Background, null, new Rect(RenderSize), 8, 8);
        if (Duration <= 0 || !double.IsFinite(Start) || !double.IsFinite(End)) return;
        double left = Math.Clamp(Start / Duration, 0, 1) * ActualWidth;
        double right = Math.Clamp(End / Duration, 0, 1) * ActualWidth;
        if (right < left) return;
        var highlight = Foreground.Clone();
        highlight.Opacity = .16;
        dc.DrawRectangle(highlight, null, new Rect(left, 0, right - left, ActualHeight));
        var pen = new Pen(Foreground, 1);
        if (Peaks is { Length: > 0 })
            for (int i = 0; i < Peaks.Length; i++)
            {
                double x = (i + .5) * ActualWidth / Peaks.Length;
                double h = Peaks[i] * (ActualHeight - 16) / 2;
                dc.DrawLine(pen, new Point(x, ActualHeight / 2 - h), new Point(x, ActualHeight / 2 + h));
            }
        dc.DrawLine(new Pen(Foreground, 2), new Point(left, 0), new Point(left, ActualHeight));
        dc.DrawLine(new Pen(Foreground, 2), new Point(right, 0), new Point(right, ActualHeight));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Duration <= 0 || ActualWidth <= 0) return;
        Focus();
        if (!CaptureMouse()) return;
        double x = e.GetPosition(this).X;
        anchor = Math.Clamp(x / ActualWidth, 0, 1) * Duration;
        handle = Math.Abs(x - Start / Duration * ActualWidth) <= 8 ? -1
            : Math.Abs(x - End / Duration * ActualWidth) <= 8 ? 1 : 0;
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured || ActualWidth <= 0) return;
        double value = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1) * Duration;
        double minimum = Math.Min(.001, Duration);
        if (handle < 0) SetCurrentValue(StartProperty, Math.Clamp(value, 0, Math.Max(0, End - minimum)));
        else if (handle > 0) SetCurrentValue(EndProperty, Math.Clamp(value, Math.Min(Duration, Start + minimum), Duration));
        else if (Math.Abs(value - anchor) >= minimum)
        {
            SetCurrentValue(StartProperty, Math.Min(anchor, value));
            SetCurrentValue(EndProperty, Math.Max(anchor, value));
        }
        e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured) { ReleaseMouseCapture(); e.Handled = true; }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Duration <= 0 || e.Key is not (Key.Left or Key.Right)) return;
        double step = Math.Min(.01, Duration) * (e.Key == Key.Left ? -1 : 1);
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            SetCurrentValue(EndProperty, Math.Clamp(End + step, Math.Min(Duration, Start + .001), Duration));
        else SetCurrentValue(StartProperty, Math.Clamp(Start + step, 0, Math.Max(0, End - .001)));
        e.Handled = true;
    }
}
