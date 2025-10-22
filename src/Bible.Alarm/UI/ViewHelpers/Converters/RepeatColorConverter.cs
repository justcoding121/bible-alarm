using System.Globalization;

namespace Bible.Alarm.UI.ViewHelpers.Converters;

public class RepeatColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return bool.Parse(value.ToString()) ? Colors.SlateBlue : Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}