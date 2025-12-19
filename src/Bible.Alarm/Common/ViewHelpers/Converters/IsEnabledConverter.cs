using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts IsEnabled boolean to theme-aware colors.
/// Enabled: Uses the parameter color (typically TextPrimaryColor for icons)
/// Disabled: Uses theme-aware disabled color (darker in dark mode, lighter in light mode)
/// </summary>
public class IsEnabledColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isEnabled = false;
        if (value is bool boolValue)
        {
            isEnabled = boolValue;
        }
        else if (bool.TryParse(value?.ToString(), out bool parsedValue))
        {
            isEnabled = parsedValue;
        }

        if (isEnabled)
        {
            // When enabled: use the parameter color (or default to TextPrimaryColor)
            if (parameter is Color enabledColor)
            {
                return enabledColor;
            }

            // Try to get from resources if parameter is a resource key string
            if (parameter is string resourceKey &&
                Application.Current?.Resources.TryGetValue(resourceKey, out var resourceValue) == true &&
                resourceValue is Color resourceColor)
            {
                return resourceColor;
            }

            // If parameter is a DynamicResource or unresolved, try common resource keys
            // This handles cases where DynamicResource in ConverterParameter doesn't resolve properly
            if (Application.Current?.Resources.TryGetValue("TextPrimaryColor", out var textPrimary) == true &&
                textPrimary is Color textPrimaryColor)
            {
                return textPrimaryColor;
            }

            // Default enabled color
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;
        }
        else
        {
            // When disabled: read from Application resources for automatic theme updates
            if (Application.Current?.Resources.TryGetValue("DisabledTextColor", out var disabledColor) == true &&
                disabledColor is Color disabledTextColor)
            {
                return disabledTextColor;
            }

            // Fallback if resource not found
            var theme = ThemeColors.GetCurrentTheme();
            return ThemeColors.Fallback.DisabledText.Get(theme);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
