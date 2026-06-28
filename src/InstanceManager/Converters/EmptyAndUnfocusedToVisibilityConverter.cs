using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InstanceManager.Converters;

public sealed class EmptyAndUnfocusedToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = values.Length > 0 && string.IsNullOrWhiteSpace(values[0] as string);
        bool isFocused = values.Length > 1 && values[1] is true;
        return isEmpty && !isFocused ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
