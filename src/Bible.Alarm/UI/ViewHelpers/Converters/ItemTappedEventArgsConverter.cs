using System.Globalization;

namespace Bible.Alarm.UI.ViewHelpers.Converters;

public class ItemTappedEventArgsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var eventArgs = value as ItemTappedEventArgs;
        if (eventArgs == null)
            throw new ArgumentException("Expected TappedEventArgs as value", "value");

        return eventArgs.Item;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}