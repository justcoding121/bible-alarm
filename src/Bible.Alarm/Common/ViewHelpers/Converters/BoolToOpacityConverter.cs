#nullable enable
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts a boolean value to opacity (1.0 for true, 0.0 for false).
/// Used for overlays to keep them in the visual tree for instant show/hide.
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 1.0 : 0.0;
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue > 0.5;
        }
        return false;
    }
}

