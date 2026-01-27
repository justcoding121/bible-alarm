using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts IsSelectable boolean to text color.
/// When selectable (true): Returns PrimaryColor (normal tappable state)
/// When not selectable (false): Returns TextPrimaryColor (matches "Reminder Enabled" text color)
/// </summary>
public sealed class SelectableTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isSelectable = false;
        if (value is bool boolValue)
        {
            isSelectable = boolValue;
        }
        else if (bool.TryParse(value?.ToString(), out bool parsedValue))
        {
            isSelectable = parsedValue;
        }

        if (isSelectable)
        {
            // When selectable: use PrimaryColor (current behavior)
            if (Application.Current?.Resources.TryGetValue("PrimaryColor", out var primaryColor) == true &&
                primaryColor is Color primary)
            {
                return primary;
            }
            // Fallback
            return Colors.Purple;
        }

        // When not selectable: use TextPrimaryColor (matches "Reminder Enabled" text)
        if (Application.Current?.Resources.TryGetValue("TextPrimaryColor", out var textPrimary) == true &&
            textPrimary is Color textPrimaryColor)
        {
            return textPrimaryColor;
        }

        // Fallback
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Colors.White
            : Colors.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
