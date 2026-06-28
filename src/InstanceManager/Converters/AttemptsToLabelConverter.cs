using System;
using System.Globalization;
using System.Windows.Data;
using InstanceManager.Models;

namespace InstanceManager.Converters;

public sealed class AttemptsToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int attempts = value switch
        {
            int i => i,
            double d => (int)Math.Round(d),
            IConvertible c => (int)Math.Round(System.Convert.ToDouble(c, culture)),
            _ => 0
        };

        return AppSettings.IsUnlimitedAttempts(attempts) ? "Unlimited" : $"{attempts} attempts";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
