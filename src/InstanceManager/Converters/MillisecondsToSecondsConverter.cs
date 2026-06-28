using System;
using System.Globalization;
using System.Windows.Data;

namespace InstanceManager.Converters;

public sealed class MillisecondsToSecondsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double ms = 0;
        if (value is double d)
        {
            ms = d;
        }
        else if (value is int i)
        {
            ms = i;
        }
        else if (value is float f)
        {
            ms = f;
        }
        else if (value is IConvertible convertible)
        {
            ms = System.Convert.ToDouble(convertible, culture);
        }

        if (ms <= 0)
            return "Instant";

        return $"{(ms / 1000.0).ToString("0.0", culture ?? CultureInfo.InvariantCulture)}s";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
