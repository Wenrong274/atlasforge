using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AtlasForge.Views.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            bool boolValue => boolValue ? Visibility.Visible : Visibility.Collapsed,
            int intValue => intValue > 0 ? Visibility.Visible : Visibility.Collapsed,
            _ => Visibility.Collapsed
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
