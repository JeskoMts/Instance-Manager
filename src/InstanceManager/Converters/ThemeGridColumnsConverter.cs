using System;
using System.Globalization;
using System.Windows.Data;

namespace InstanceManager.Converters;

public sealed class ThemeGridColumnsConverter : IValueConverter
{
    public static int ColumnsForWidth(double width) =>
        !double.IsFinite(width) || width < 560 ? 2 :
        width < 800 ? 3 :
        4;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ColumnsForWidth(value is double width ? width : 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
