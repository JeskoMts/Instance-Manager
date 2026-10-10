using System;
using System.Globalization;
using System.Windows.Data;

namespace InstanceManager.Converters;

public sealed class GameGridColumnsConverter : IValueConverter
{
    public const double MinCardWidth = 240;
    public const double Gap = 12;
    public const int MaxColumns = 8;

    public static int ColumnsForWidth(double width) =>
        !double.IsFinite(width) || width <= 0
            ? 1
            : Math.Clamp((int)Math.Floor((width + Gap) / (MinCardWidth + Gap)), 1, MaxColumns);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ColumnsForWidth(value is double width ? width : 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
