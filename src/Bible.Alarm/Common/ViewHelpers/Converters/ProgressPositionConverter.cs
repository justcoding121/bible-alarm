using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts a progress percentage (0.0 to 1.0) to a pixel offset for TranslationX.
/// Assumes the parent container width is approximately 400 pixels (accounting for margins).
/// </summary>
public class ProgressPositionConverter : IValueConverter
{
    private const double EstimatedParentWidth = 400.0; // Approximate width accounting for margins

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            // Convert percentage (0.0 to 1.0) to pixel offset for TranslationX
            // Account for segment width (30% = 120px) so it doesn't go off screen
            var maxOffset = EstimatedParentWidth * (1.0 - 0.3); // Leave room for segment width
            return percentage * maxOffset;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

