using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts IsSelected boolean to theme-aware background colors.
/// Reads from Application.Current.Resources which are updated on theme changes.
/// Note: This converter re-evaluates when the bound IsSelected property changes,
/// but to update on theme changes, the CollectionView must be refreshed.
/// For a fully automatic solution, consider using VisualStateManager with CollectionView.SelectedItem.
/// </summary>
public class IsSelectedColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return GetUnselectedColor();
        }

        bool isSelected = false;
        if (value is bool boolValue)
        {
            isSelected = boolValue;
        }
        else if (bool.TryParse(value.ToString(), out bool parsedValue))
        {
            isSelected = parsedValue;
        }

        // Return theme-aware colors from Application resources
        // Selected: Use ControlBackgroundColor (lighter background for selected state)
        // Unselected: Use CardBackgroundColor (standard card background)
        return isSelected ? GetSelectedColor() : GetUnselectedColor();
    }

    private Color GetSelectedColor()
    {
        // Read from Application resources - these are updated by App.xaml.cs on theme change
        if (Application.Current?.Resources.TryGetValue("ControlBackgroundColor", out var controlBgColor) == true &&
            controlBgColor is Color selectedColor)
        {
            return selectedColor;
        }

        // Fallback if resource not found
        var theme = ThemeColors.GetCurrentTheme();
        return ThemeColors.ControlBackground.Get(theme);
    }

    private Color GetUnselectedColor()
    {
        // Read from Application resources - these are updated by App.xaml.cs on theme change
        if (Application.Current?.Resources.TryGetValue("CardBackgroundColor", out var cardBgColor) == true &&
            cardBgColor is Color unselectedColor)
        {
            return unselectedColor;
        }

        // Fallback if resource not found
        var theme = ThemeColors.GetCurrentTheme();
        return ThemeColors.CardBackground.Get(theme);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
