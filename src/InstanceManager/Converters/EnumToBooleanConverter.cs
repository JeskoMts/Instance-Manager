using System;
using System.Globalization;
using System.Windows.Data;

namespace InstanceManager.Converters;

public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter is not string name)
            return false;
        return string.Equals(value.ToString(), name, StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string name && targetType.IsEnum)
            return Enum.Parse(targetType, name);
        return Binding.DoNothing;
    }
}
