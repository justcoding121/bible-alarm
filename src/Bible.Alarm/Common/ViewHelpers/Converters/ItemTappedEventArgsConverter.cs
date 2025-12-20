using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class ItemTappedEventArgsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Handle SelectionChangedEventArgs from CollectionView
        if (value is SelectionChangedEventArgs selectionChangedEventArgs)
        {
            // Return the first selected item (CollectionView supports single selection in our use case)
            return selectionChangedEventArgs.CurrentSelection?.FirstOrDefault();
        }

        // Handle ItemTappedEventArgs from ListView (for backward compatibility)
        if (value is ItemTappedEventArgs itemTappedEventArgs)
        {
            return itemTappedEventArgs.Item;
        }

        throw new ArgumentException($"Expected SelectionChangedEventArgs or ItemTappedEventArgs as value, got {value?.GetType().Name}", "value");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
