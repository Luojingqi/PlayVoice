using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
namespace PlayVoice;

public sealed class WaveformEditor : Control
{
    public float[] Peaks { get; set; } = [];
    public double Start { get; set; }
    public double End { get; set; } = 1;
    private double anchor;
    public event EventHandler SelectionChanged;
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var brush = this.FindResource("AccentColor") as IBrush ?? Brushes.DodgerBlue;
        var pen = new Pen(brush, 1);
        for (int i = 0; i < Peaks.Length; i++) { double x = i * Bounds.Width / Peaks.Length; double h = Peaks[i] * Bounds.Height * .45; context.DrawLine(pen, new Point(x, Bounds.Height / 2 - h), new Point(x, Bounds.Height / 2 + h)); }
        context.DrawRectangle(new SolidColorBrush(Colors.DodgerBlue, .14), null, new Rect(Start * Bounds.Width, 0, Math.Max(0, (End - Start) * Bounds.Width), Bounds.Height));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Left or Key.Right)) return;
        double delta = e.Key == Key.Left ? -.005 : .005;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) End = Math.Clamp(End + delta, Start + .001, 1);
        else Start = Math.Clamp(Start + delta, 0, End - .001);
        InvalidateVisual(); SelectionChanged?.Invoke(this, EventArgs.Empty); e.Handled = true;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); anchor = Math.Clamp(e.GetPosition(this).X / Math.Max(1, Bounds.Width), 0, 1); e.Pointer.Capture(this); }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (e.Pointer.Captured != this) return; var p = Math.Clamp(e.GetPosition(this).X / Math.Max(1, Bounds.Width), 0, 1); Start = Math.Min(anchor, p); End = Math.Max(anchor, p); InvalidateVisual(); SelectionChanged?.Invoke(this, EventArgs.Empty); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { e.Pointer.Capture(null); base.OnPointerReleased(e); }
}
