#nullable enable annotations

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PlayVoice.Audio;

namespace PlayVoice.Pages.Preset;

public sealed class WaveformSelection : Control
{
    private const double WaveformInset = 14;
    private const double LabelLaneHeight = 25;
    private const double HandleRadius = 6.5;
    private const double HandleHitRadius = 13;

    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(nameof(Peaks), typeof(float[]), typeof(WaveformSelection), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(nameof(Start), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty FadeInProperty = DependencyProperty.Register(nameof(FadeIn), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty FadeOutProperty = DependencyProperty.Register(nameof(FadeOut), typeof(double), typeof(WaveformSelection), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty FadeInLabelProperty = DependencyProperty.Register(nameof(FadeInLabel), typeof(string), typeof(WaveformSelection), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FadeOutLabelProperty = DependencyProperty.Register(nameof(FadeOutLabel), typeof(string), typeof(WaveformSelection), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty AxisBrushProperty = DependencyProperty.Register(nameof(AxisBrush), typeof(Brush), typeof(WaveformSelection), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty BadgeTextBrushProperty = DependencyProperty.Register(nameof(BadgeTextBrush), typeof(Brush), typeof(WaveformSelection), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public float[]? Peaks { get => (float[]?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public double Start { get => (double)GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public double End { get => (double)GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public double FadeIn { get => (double)GetValue(FadeInProperty); set => SetValue(FadeInProperty, value); }
    public double FadeOut { get => (double)GetValue(FadeOutProperty); set => SetValue(FadeOutProperty, value); }
    public string FadeInLabel { get => (string)GetValue(FadeInLabelProperty); set => SetValue(FadeInLabelProperty, value); }
    public string FadeOutLabel { get => (string)GetValue(FadeOutLabelProperty); set => SetValue(FadeOutLabelProperty, value); }
    private Brush AxisBrush => (Brush)GetValue(AxisBrushProperty);
    private Brush BadgeTextBrush => (Brush)GetValue(BadgeTextBrushProperty);

    internal enum Handle { None, Range, TrimStart, TrimEnd, FadeIn, FadeOut }

    private double anchor;
    private double fadeDragOffset;
    private bool pointerChanged;
    private Handle pointerHandle;
    private Handle keyboardHandle;
    private Handle hoverHandle;
    internal event Action<Handle>? EditCompleted;

    private Rect Plot => new(WaveformInset, LabelLaneHeight, Math.Max(0, ActualWidth - 2 * WaveformInset), Math.Max(0, ActualHeight - 2 * LabelLaneHeight));
    private bool HasTimeScale => double.IsFinite(Duration) && Duration > 0 && Plot.Width > 0 && Plot.Height > 0;
    private bool HasSelection => double.IsFinite(Start) && double.IsFinite(End) && Start >= 0 && End > Start && End <= Duration;

    public WaveformSelection()
    {
        Focusable = true;
        MinHeight = 128;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        SetResourceReference(ForegroundProperty, "AccentColor");
        SetResourceReference(BackgroundProperty, "BgDark");
        SetResourceReference(AxisBrushProperty, "TextSecondary");
        SetResourceReference(BadgeTextBrushProperty, "AccentForeground");
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Background, null, new Rect(RenderSize), 8, 8);
        if (!HasTimeScale) return;

        var plot = Plot;
        double center = plot.Top + plot.Height / 2;
        double left = HasSelection ? XAt(Start, plot) : plot.Left;
        double right = HasSelection ? XAt(End, plot) : plot.Right;
        double fadeInX = XAt(FadePoint(Handle.FadeIn), plot);
        double fadeOutX = XAt(FadePoint(Handle.FadeOut), plot);
        var active = pointerHandle != Handle.None ? pointerHandle : keyboardHandle != Handle.None ? keyboardHandle : hoverHandle;
        if (HasSelection)
        {
            dc.DrawRectangle(WithOpacity(Foreground, .08), null, new Rect(left, plot.Top, right - left, plot.Height));
            var fadeFill = WithOpacity(Foreground, .14);
            dc.DrawRectangle(fadeFill, null, new Rect(left, plot.Top, Math.Max(0, fadeInX - left), plot.Height));
            dc.DrawRectangle(fadeFill, null, new Rect(fadeOutX, plot.Top, Math.Max(0, right - fadeOutX), plot.Height));
        }

        dc.DrawLine(new Pen(WithOpacity(AxisBrush, .35), 1), new Point(plot.Left, center), new Point(plot.Right, center));
        DrawPeaks(dc, plot, left, right, center);
        DrawTimeline(dc, plot);
        if (!HasSelection) return;

        DrawGain(dc, plot, left, right);
        var trimPen = new Pen(AxisBrush, 2);
        dc.DrawLine(trimPen, new Point(left, plot.Top), new Point(left, plot.Bottom));
        dc.DrawLine(trimPen, new Point(right, plot.Top), new Point(right, plot.Bottom));

        DrawFadeGuide(dc, plot, fadeInX, active == Handle.FadeIn);
        DrawFadeGuide(dc, plot, fadeOutX, active == Handle.FadeOut);
        bool split = Math.Abs(fadeInX - fadeOutX) < 2 * HandleRadius;
        DrawFadeHandle(dc, new Point(fadeInX, center), Handle.FadeIn, split, active);
        DrawFadeHandle(dc, new Point(fadeOutX, center), Handle.FadeOut, split, active);
        if (IsFade(active))
            DrawTimeBadge(dc, active == Handle.FadeIn ? fadeInX : fadeOutX, active);
        else
            DrawFadeTimes(dc, fadeInX, fadeOutX);
    }

    private void DrawPeaks(DrawingContext dc, Rect plot, double left, double right, double center)
    {
        if (Peaks is not { Length: > 0 } peaks) return;

        int bars = Math.Min(peaks.Length, Math.Max(1, (int)(plot.Width / 2)));
        var pen = new Pen(Foreground, 1);
        var dimmedPen = new Pen(WithOpacity(AxisBrush, .42), 1);
        for (int bar = 0; bar < bars; bar++)
        {
            double x = plot.Left + (bar + .5) * plot.Width / bars;
            float peak = 0;
            for (int bin = bar * peaks.Length / bars; bin < (bar + 1) * peaks.Length / bars; bin++)
                peak = Math.Max(peak, peaks[bin]);
            double halfHeight = Math.Clamp(peak, 0, 1) * (plot.Height - 8) / 2;
            dc.DrawLine(x >= left && x <= right ? pen : dimmedPen,
                new Point(x, center - halfHeight), new Point(x, center + halfHeight));
        }
    }

    private void DrawGain(DrawingContext dc, Rect plot, double left, double right)
    {
        var (fadeIn, fadeOut) = GetFadeLengths();
        if (fadeIn == 0 && fadeOut == 0) return;

        int steps = Math.Max(1, (int)Math.Ceiling((right - left) / 2));
        var gain = new StreamGeometry();
        using (var line = gain.Open())
        {
            for (int i = 0; i <= steps; i++)
            {
                double elapsed = (End - Start) * i / steps;
                double inGain = fadeIn > 0 ? Math.Min(1, elapsed / fadeIn) : 1;
                double outGain = fadeOut > 0 ? Math.Min(1, (End - Start - elapsed) / fadeOut) : 1;
                var point = new Point(left + (right - left) * i / steps,
                    plot.Bottom - 3 - (plot.Height - 6) * inGain * outGain);
                if (i == 0) line.BeginFigure(point, false, false);
                else line.LineTo(point, true, false);
            }
        }
        dc.DrawGeometry(null, new Pen(Foreground, 1.5), gain);
    }

    private void DrawTimeline(DrawingContext dc, Rect plot)
    {
        var first = CreateText("0", AxisBrush);
        var middle = CreateText(FormatSeconds(Duration / 2, "0.###"), AxisBrush);
        var last = CreateText(FormatSeconds(Duration, "0.###"), AxisBrush);
        double centerX = plot.Left + plot.Width / 2;
        double lastX = Math.Max(plot.Left, plot.Right - last.Width);
        var tick = new Pen(AxisBrush, 1);
        dc.DrawLine(tick, new Point(plot.Left, plot.Bottom + 3), new Point(plot.Left, plot.Bottom + 6));
        dc.DrawLine(tick, new Point(plot.Right, plot.Bottom + 3), new Point(plot.Right, plot.Bottom + 6));
        dc.DrawText(first, new Point(plot.Left, plot.Bottom + 7));
        if (lastX > plot.Left + first.Width + 6)
            dc.DrawText(last, new Point(lastX, plot.Bottom + 7));
        if (centerX - middle.Width / 2 > plot.Left + first.Width + 6 && centerX + middle.Width / 2 < lastX - 6)
        {
            dc.DrawLine(tick, new Point(centerX, plot.Bottom + 3), new Point(centerX, plot.Bottom + 6));
            dc.DrawText(middle, new Point(centerX - middle.Width / 2, plot.Bottom + 7));
        }
    }

    private void DrawFadeGuide(DrawingContext dc, Rect plot, double x, bool active)
    {
        var guide = new Pen(WithOpacity(Foreground, active ? 1 : .7), active ? 1.5 : 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(guide, new Point(x, plot.Top), new Point(x, plot.Bottom + 4));
    }

    private void DrawFadeHandle(DrawingContext dc, Point point, Handle handle, bool split, Handle active)
    {
        const double haloRadius = HandleRadius + 4;
        if (split)
        {
            double top = handle == Handle.FadeIn ? point.Y - haloRadius : point.Y;
            dc.PushClip(new RectangleGeometry(new Rect(point.X - haloRadius, top, haloRadius * 2, haloRadius)));
        }
        if (handle == active)
            dc.DrawEllipse(WithOpacity(Foreground, .22), null, point, haloRadius, haloRadius);
        dc.DrawEllipse(handle == active ? Foreground : Background, new Pen(Foreground, 2), point, HandleRadius, HandleRadius);
        if (split) dc.Pop();
    }

    private void DrawTimeBadge(DrawingContext dc, double x, Handle handle)
    {
        const double padding = 6;
        string label = handle == Handle.FadeIn ? FadeInLabel : FadeOutLabel;
        var text = CreateText($"{label}  {FormatSeconds(FadePoint(handle), "0.000")}", BadgeTextBrush);
        text.MaxTextWidth = Math.Max(1, ActualWidth - 2 * padding - 4);
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;
        double width = text.Width + 2 * padding;
        double left = Math.Clamp(x - width / 2, 2, Math.Max(2, ActualWidth - width - 2));
        dc.DrawRoundedRectangle(Foreground, null, new Rect(left, 3, width, text.Height + 4), 4, 4);
        dc.DrawText(text, new Point(left + padding, 5));
    }

    private void DrawFadeTimes(DrawingContext dc, double fadeInX, double fadeOutX)
    {
        var (fadeIn, fadeOut) = GetFadeLengths();
        if (fadeIn == 0 && fadeOut == 0) return;
        var first = CreateText(FormatSeconds(FadePoint(Handle.FadeIn), "0.000"), AxisBrush);
        var second = CreateText(FormatSeconds(FadePoint(Handle.FadeOut), "0.000"), AxisBrush);
        const double gap = 6;
        double firstMax = ActualWidth - first.Width - second.Width - 2 * gap;
        if (firstMax < gap) return;
        double firstLeft = Math.Clamp(fadeInX - first.Width / 2, gap, firstMax);
        double secondLeft = Math.Min(ActualWidth - second.Width - gap,
            Math.Max(fadeOutX - second.Width / 2, firstLeft + first.Width + gap));
        dc.DrawText(first, new Point(firstLeft, 6));
        dc.DrawText(second, new Point(secondLeft, 6));
    }

    private FormattedText CreateText(string text, Brush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            11, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static string FormatSeconds(double seconds, string format) => seconds.ToString(format, CultureInfo.CurrentCulture) + " s";

    private static Brush WithOpacity(Brush brush, double opacity)
    {
        var copy = brush.CloneCurrentValue();
        copy.Opacity *= opacity;
        return copy;
    }

    private double XAt(double time, Rect plot) => plot.Left + time / Duration * plot.Width;
    private double TimeAt(double x, Rect plot) => Math.Clamp((x - plot.Left) / plot.Width, 0, 1) * Duration;
    private static bool IsFade(Handle handle) => handle is Handle.FadeIn or Handle.FadeOut;
    private (double FadeIn, double FadeOut) GetFadeLengths()
    {
        if (!HasSelection) return (0, 0);
        double fadeIn = double.IsFinite(FadeIn) ? Math.Max(0, FadeIn) : 0;
        double fadeOut = double.IsFinite(FadeOut) ? Math.Max(0, FadeOut) : 0;
        return AudioEditSettings.FitFadesToDuration(fadeIn, fadeOut, End - Start);
    }

    private double FadePoint(Handle handle)
    {
        if (!HasSelection) return 0;
        var (fadeIn, fadeOut) = GetFadeLengths();
        double fadeInEnd = Math.Min(End, Start + fadeIn);
        return handle == Handle.FadeIn ? fadeInEnd : Math.Max(fadeInEnd, End - fadeOut);
    }

    private Handle HitFadeHandle(Point point)
    {
        if (!HasTimeScale || !HasSelection || !IsEnabled) return Handle.None;

        var plot = Plot;
        double relativeY = point.Y - (plot.Top + plot.Height / 2);
        if (Math.Abs(relativeY) > HandleHitRadius) return Handle.None;
        double fadeInX = XAt(FadePoint(Handle.FadeIn), plot);
        double fadeOutX = XAt(FadePoint(Handle.FadeOut), plot);
        double distanceToIn = Math.Abs(point.X - fadeInX);
        double distanceToOut = Math.Abs(point.X - fadeOutX);
        if (distanceToIn <= HandleHitRadius && distanceToOut <= HandleHitRadius && Math.Abs(fadeInX - fadeOutX) < 2 * HandleRadius)
            return relativeY <= 0 ? Handle.FadeIn : Handle.FadeOut;
        if (Math.Min(distanceToIn, distanceToOut) > HandleHitRadius) return Handle.None;
        return distanceToIn <= distanceToOut ? Handle.FadeIn : Handle.FadeOut;
    }

    private bool SetFadeLength(Handle handle, double seconds)
    {
        var property = handle == Handle.FadeIn ? FadeInProperty : FadeOutProperty;
        var (fadeIn, fadeOut) = GetFadeLengths();
        double otherFade = handle == Handle.FadeIn ? fadeOut : fadeIn;
        double available = AudioEditSettings.RemainingFadeTime(End - Start, otherFade);
        double value = Math.Clamp(seconds, 0, available);
        if (value > 0 && value < available && available >= .001)
            value = Math.Min(Math.Round(value, 3), available);
        if ((double)GetValue(property) == value) return false;
        SetCurrentValue(property, value);
        return true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!HasTimeScale) return;
        Focus();
        FinishKeyboardEdit();
        if (!HasTimeScale || !CaptureMouse()) return;

        var point = e.GetPosition(this);
        var plot = Plot;
        anchor = TimeAt(point.X, plot);
        pointerHandle = HitFadeHandle(point);
        if (IsFade(pointerHandle))
            fadeDragOffset = point.X - XAt(FadePoint(pointerHandle), plot);
        else
            pointerHandle = HitTrimHandle(point.X, plot);
        pointerChanged = false;
        Cursor = IsFade(pointerHandle) ? Cursors.SizeWE : Cursors.Cross;
        InvalidateVisual();
        e.Handled = true;
    }

    private Handle HitTrimHandle(double x, Rect plot)
    {
        if (!HasSelection) return Handle.Range;
        double left = Math.Abs(x - XAt(Start, plot));
        double right = Math.Abs(x - XAt(End, plot));
        if (Math.Min(left, right) > 8) return Handle.Range;
        return left <= right ? Handle.TrimStart : Handle.TrimEnd;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (pointerHandle == Handle.None)
        {
            SetHoverHandle(HitFadeHandle(point));
            return;
        }
        if (!IsMouseCaptured || !HasTimeScale) return;

        var plot = Plot;
        if (IsFade(pointerHandle))
        {
            if (HasSelection)
            {
                double x = point.X - fadeDragOffset;
                if (Math.Abs(x - XAt(FadePoint(pointerHandle), plot)) > .001)
                    pointerChanged |= MoveHandle(pointerHandle, TimeAt(x, plot));
            }
        }
        else
        {
            pointerChanged |= MoveHandle(pointerHandle, TimeAt(point.X, plot));
        }
        e.Handled = true;
    }

    private bool MoveHandle(Handle handle, double time)
    {
        double minimum = Math.Min(.001, Duration);
        switch (handle)
        {
            case Handle.FadeIn:
                return SetFadeLength(handle, time - Start);
            case Handle.FadeOut:
                return SetFadeLength(handle, End - time);
            case Handle.TrimStart:
                return SetTrimValue(StartProperty, Math.Clamp(time, 0, Math.Max(0, End - minimum)));
            case Handle.TrimEnd:
                return SetTrimValue(EndProperty, Math.Clamp(time, Math.Min(Duration, Start + minimum), Duration));
            case Handle.Range when Math.Abs(time - anchor) >= minimum:
                bool startChanged = SetTrimValue(StartProperty, Math.Min(anchor, time));
                bool endChanged = SetTrimValue(EndProperty, Math.Max(anchor, time));
                return startChanged || endChanged;
            default:
                return false;
        }
    }

    private bool SetTrimValue(DependencyProperty property, double value)
    {
        if ((double)GetValue(property) == value) return false;
        SetCurrentValue(property, value);
        return true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoverHandle(Handle.None);
    }

    private void SetHoverHandle(Handle handle)
    {
        if (hoverHandle == handle) return;
        hoverHandle = handle;
        Cursor = IsFade(handle) || IsFade(pointerHandle) ? Cursors.SizeWE : Cursors.Cross;
        InvalidateVisual();
    }

    protected override void OnToolTipOpening(ToolTipEventArgs e)
    {
        if (IsFade(hoverHandle) || IsFade(pointerHandle) || IsFade(keyboardHandle))
            e.Handled = true;
        base.OnToolTipOpening(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (pointerHandle != Handle.None && IsMouseCaptured)
        {
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        var completed = pointerHandle;
        if (completed == Handle.None) return;
        pointerHandle = Handle.None;
        var hover = IsMouseOver ? HitFadeHandle(Mouse.GetPosition(this)) : Handle.None;
        SetHoverHandle(hover);
        Cursor = IsFade(hover) ? Cursors.SizeWE : Cursors.Cross;
        InvalidateVisual();
        if (pointerChanged) EditCompleted?.Invoke(completed);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasTimeScale || pointerHandle != Handle.None || e.Key is not (Key.Left or Key.Right)) return;
        var modifiers = Keyboard.Modifiers;
        bool shift = (modifiers & ModifierKeys.Shift) != 0;
        bool control = (modifiers & ModifierKeys.Control) != 0;
        var handle = control
            ? shift ? Handle.FadeOut : Handle.FadeIn
            : shift ? Handle.TrimEnd : Handle.TrimStart;
        if (control ? !HasSelection : !double.IsFinite(Start) || !double.IsFinite(End)) return;
        if (keyboardHandle != handle) FinishKeyboardEdit();
        double step = Math.Min(.01, Duration) * (e.Key == Key.Left ? -1 : 1);
        double time = IsFade(handle) ? FadePoint(handle) : shift ? End : Start;
        if (MoveHandle(handle, time + step))
        {
            keyboardHandle = handle;
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.Left or Key.Right && !Keyboard.IsKeyDown(Key.Left) && !Keyboard.IsKeyDown(Key.Right))
            FinishKeyboardEdit();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        FinishKeyboardEdit();
    }

    internal void FinishEditing()
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        FinishKeyboardEdit();
    }

    private void FinishKeyboardEdit()
    {
        var completed = keyboardHandle;
        if (completed == Handle.None) return;
        keyboardHandle = Handle.None;
        InvalidateVisual();
        EditCompleted?.Invoke(completed);
    }
}
