using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PlayVoice.Pages;

internal static class ClickRippleEffect
{
    private const double CornerRadius = 3;

    public static void UpdateClip(Canvas canvas, Size size)
    {
        canvas.Clip = new RectangleGeometry(
            new Rect(0, 0, size.Width, size.Height),
            CornerRadius,
            CornerRadius);
    }

    public static void Start(Canvas canvas, Point origin)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        origin.X = Math.Clamp(origin.X, 0, width);
        origin.Y = Math.Clamp(origin.Y, 0, height);

        double horizontalRadius = Math.Max(origin.X, width - origin.X);
        double verticalRadius = Math.Max(origin.Y, height - origin.Y);
        double diameter = Math.Sqrt(
            horizontalRadius * horizontalRadius +
            verticalRadius * verticalRadius) * 2;

        var scale = new ScaleTransform(0, 0);
        var ripple = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Opacity = 0.28,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = scale
        };
        ripple.SetResourceReference(Shape.FillProperty, "AccentColorSecondary");

        Canvas.SetLeft(ripple, origin.X - diameter / 2);
        Canvas.SetTop(ripple, origin.Y - diameter / 2);
        canvas.Children.Add(ripple);

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var expansion = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = easing
        };
        var fade = new DoubleAnimation(0.28, 0, TimeSpan.FromMilliseconds(360))
        {
            BeginTime = TimeSpan.FromMilliseconds(90),
            EasingFunction = easing
        };
        fade.Completed += (_, _) => canvas.Children.Remove(ripple);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, expansion);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, expansion);
        ripple.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
