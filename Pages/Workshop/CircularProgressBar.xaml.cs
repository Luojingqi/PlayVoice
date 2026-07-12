using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.Workshop;

/// <summary>
/// CircularProgressBar.xaml 的交互逻辑
/// </summary>
public partial class CircularProgressBar : UserControl
{
    public CircularProgressBar()
    {
        InitializeComponent();
        DrawProgress(0);
    }

    /// <summary>
    /// 输入 0.0 到 1.0 的值，更新进度
    /// </summary>
    /// <param name="value">范围 0.0 - 1.0</param>
    public void SetProgress(double value,int duration = 300)
    {
        DoubleAnimation animation = new DoubleAnimation
        {
            To = value,   
            Duration = TimeSpan.FromMilliseconds(duration), 

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        this.BeginAnimation(ProgressValueProperty, animation);
    }

    public static readonly DependencyProperty ProgressValueProperty =
        DependencyProperty.Register(
            "ProgressValue",
            typeof(double),
            typeof(CircularProgressBar),
            new PropertyMetadata(0.0, OnProgressValueChanged));

    public double ProgressValue
    {
        get { return (double)GetValue(ProgressValueProperty); }
        set { SetValue(ProgressValueProperty, value); }
    }

    private static void OnProgressValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularProgressBar control)
        {
            control.DrawProgress((double)e.NewValue);
        }
    }

    private void DrawProgress(double value)
    {
        value = Math.Max(0.0, Math.Min(1.0, value));

        PercentText.Text = $"{value * 100:0.#}%";

        if (value <= 0)
        {
            ProgressPath.Data = null;
            return;
        }
        if (value >= 1)
        {
            ProgressPath.Data = Geometry.Parse("M 50,5 A 45,45 0 1 1 49.99,5");
            return;
        }

        double radius = 45;      // 半径
        double centerX = 50;     // 圆心 X
        double centerY = 50;     // 圆心 Y

        double angle = value * 360.0;

        double angleRad = (angle - 90) * Math.PI / 180.0;

        double endX = centerX + radius * Math.Cos(angleRad);
        double endY = centerY + radius * Math.Sin(angleRad);

        int isLargeArc = angle > 180.0 ? 1 : 0;

        string pathData = $"M 50,5 A 45,45 0 {isLargeArc} 1 {endX.ToString(CultureInfo.InvariantCulture)},{endY.ToString(CultureInfo.InvariantCulture)}";

        ProgressPath.Data = Geometry.Parse(pathData);
    }
}
