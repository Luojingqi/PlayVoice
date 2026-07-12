using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlayVoice.Pages.ToggleSwitchButton;

public class HeightToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double ratio = 0.5; // 默认半圆
        if (parameter is string s && double.TryParse(s, out double p))
            ratio = p;

        if (value is double height)
        {
            double radius = height * ratio;
            return new CornerRadius(radius);
        }
        return new CornerRadius(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}