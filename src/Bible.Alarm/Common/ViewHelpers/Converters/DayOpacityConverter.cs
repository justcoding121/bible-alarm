using System.Globalization;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public sealed class DayOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return 1.0;
        }

        var schedule = value as ScheduleListItem;
        if (schedule == null)
        {
            return 1.0;
        }

        // Dim the day indicators when alarm is disabled
        return schedule.IsEnabled ? 1.0 : 0.6;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

