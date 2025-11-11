using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class DayColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Colors.LightGray;

        if (value is DaysOfWeek)
        {
            var isEnabled = ((DaysOfWeek)value & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;
            return isEnabled ? Colors.SlateBlue : Colors.LightGray;
        }
        else
        {
            var schedule = value as ScheduleListItem;

            var isEnabled = (schedule.DaysOfWeek & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;

            if (schedule.IsEnabled)
                return isEnabled ? Colors.SlateBlue : Colors.LightGray;
            return isEnabled ? Colors.LightGray : Colors.WhiteSmoke;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}