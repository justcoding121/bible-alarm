using System.Globalization;
using Bible.Alarm.Views;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts FlowDirection to the appropriate arrow glyph for RTL/LTR layouts.
/// Returns Left arrow for RTL, Right arrow for LTR.
/// </summary>
public sealed class FlowDirectionToArrowConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FlowDirection flowDirection)
        {
            return flowDirection == FlowDirection.RightToLeft
                ? GlyphNames.Left
                : GlyphNames.Right;
        }

        // Default to right arrow for LTR
        return GlyphNames.Right;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
