using System.Globalization;
using System.Windows.Data;

namespace PlayVoice.Pages.ToggleSwitchButton;

public class HeightToThumbSizeConverter : IValueConverter
{
    public double Ratio { get; set; } = 0.8;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double height)
            return height * Ratio;
        return 20.0; // 默认值
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
