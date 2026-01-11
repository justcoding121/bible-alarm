#nullable enable
using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts a double value to a Thickness with the value as the bottom margin.
/// Used for dynamic bottom margins based on floating button visibility.
/// </summary>
public sealed class DoubleToBottomThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return new Thickness(0, 0, 0, doubleValue);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Thickness thickness)
        {
            return thickness.Bottom;
        }
        return 0.0;
    }
}
