using System.Globalization;

namespace Bible.Alarm.UI.ViewHelpers.Converters;

public class IsEnabledColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is Color) return bool.Parse(value.ToString()) ? (Color)parameter : Colors.LightGray;

        return bool.Parse(value.ToString()) ? Colors.Black : Colors.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}