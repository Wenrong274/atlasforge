using System.Globalization;
using System.Windows.Data;

using AtlasForge.Models;

namespace AtlasForge.Views.Converters;

public class PackingModeToBoolConverter : IValueConverter
{
    public PackingMode TrueValue { get; set; } = PackingMode.Grid;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is PackingMode mode && mode == TrueValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true
            ? TrueValue
            : TrueValue == PackingMode.Grid
                ? PackingMode.BinPack
                : PackingMode.Grid;
}