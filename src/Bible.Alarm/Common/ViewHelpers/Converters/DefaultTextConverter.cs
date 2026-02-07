#nullable enable
using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts text value, returning a default value if the input is null or empty.
/// Used to ensure Labels always have content for consistent layout measurement.
/// </summary>
public sealed class DefaultTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return str;
        }
        
        // Return default value from parameter, or non-breaking space if no parameter provided
        if (parameter is string defaultText)
        {
            return defaultText;
        }
        
        // Use non-breaking space to reserve space when text is empty
        return "\u00A0";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Not used for one-way binding
        return value;
    }
}
