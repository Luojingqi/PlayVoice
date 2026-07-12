using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PlayVoice.Pages.HorizontalButtonGroup
{
    public class IsLastItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DependencyObject item)
            {
                // 查找当前 Item 所属的 ItemsControl (ListBox)
                var itemsControl = ItemsControl.ItemsControlFromItemContainer(item);
                if (itemsControl != null)
                {
                    // 获取当前项的索引
                    int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
                    // 判断是否为最后一项
                    return index == itemsControl.Items.Count - 1;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
