using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InstanceManager.Converters;

public sealed class ThemeCardVisibilityConverter : IMultiValueConverter
{
    public int PrimaryCount { get; set; } = 8;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        int index = values.Length > 0 && values[0] is int i ? i : 0;
        bool showAll = values.Length > 1 && values[1] is bool b && b;
        return index < PrimaryCount || showAll ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
