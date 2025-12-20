using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public sealed class ItemTappedEventArgsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            // Handle SelectionChangedEventArgs from CollectionView
            SelectionChangedEventArgs selectionChangedEventArgs => selectionChangedEventArgs.CurrentSelection
                ?.FirstOrDefault(),
            // Handle ItemTappedEventArgs from ListView (for backward compatibility)
            ItemTappedEventArgs itemTappedEventArgs => itemTappedEventArgs.Item,
            _ => throw new ArgumentException(
                $"Expected SelectionChangedEventArgs or ItemTappedEventArgs as value, got {value?.GetType().Name}",
                nameof(value))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
