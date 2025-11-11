using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class ItemTappedEventArgsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ItemTappedEventArgs eventArgs)
            throw new ArgumentException("Expected TappedEventArgs as value", "value");

        return eventArgs.Item;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}