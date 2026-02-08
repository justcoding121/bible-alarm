#nullable enable
using System.Globalization;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts a string direction ("rtl"/"ltr") to FlowDirection enum.
/// Used for list items that need individual RTL/LTR layout based on language direction.
/// </summary>
public sealed class StringToFlowDirectionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string direction)
        {
            return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }

        // Default to LTR
        return FlowDirection.LeftToRight;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
