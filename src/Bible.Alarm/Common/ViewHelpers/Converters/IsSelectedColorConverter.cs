using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class IsSelectedColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return bool.Parse(value.ToString()) ? Colors.LightGray : Colors.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}