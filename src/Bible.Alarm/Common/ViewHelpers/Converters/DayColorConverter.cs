using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class DayColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Colors.White;

        if (value is DaysOfWeek)
        {
            var isEnabled = ((DaysOfWeek)value & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;
            return isEnabled ? Colors.White : Color.FromArgb("#666666");
        }
        else
        {
            var schedule = value as ScheduleListItem;

            var isEnabled = (schedule.DaysOfWeek & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;

        if (schedule.IsEnabled)
        {
            // When alarm is enabled: light text on dark background for enabled days, darker text for disabled days
            return isEnabled ? Colors.White : Color.FromArgb("#666666");
        }
        else
        {
            // When alarm is disabled: flip the colors - enabled days get white text (on blue), disabled days get darker text (on gray)
            return isEnabled ? Colors.White : Color.FromArgb("#666666"); // White for enabled days, darker text for disabled days
        }
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}