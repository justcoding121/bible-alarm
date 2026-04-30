using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts IsSelectable boolean to text color.
/// Always returns PrimaryColor regardless of selectable state.
/// </summary>
public sealed class SelectableTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            // Always return PrimaryColor regardless of selectable state
            if (Application.Current?.Resources.TryGetValue("PrimaryColor", out var primaryColor) is true &&
                primaryColor is Color primary)
            {
                return primary;
            }
            // Fallback
            return Colors.Purple;
        }
        catch (Exception)
        {
            // Return safe fallback on any error
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
