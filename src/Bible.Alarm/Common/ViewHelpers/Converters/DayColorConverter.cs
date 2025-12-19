using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class DayColorConverter : IValueConverter
{
    private static DaysOfWeek ParseDayParameter(object parameter)
    {
        if (parameter == null)
        {
            return (DaysOfWeek)0;
        }

        if (parameter is DaysOfWeek day)
        {
            return day;
        }

        if (parameter is string dayString && Enum.TryParse<DaysOfWeek>(dayString, out var parsedDay))
        {
            return parsedDay;
        }

        return (DaysOfWeek)0;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return Colors.White;
        }

        var dayParameter = ParseDayParameter(parameter);

        if (value is DaysOfWeek)
        {
            var isEnabled = ((DaysOfWeek)value & dayParameter) == dayParameter;
            return isEnabled ? ThemeColors.Day.EnabledText : ThemeColors.Day.DisabledText;
        }
        else
        {
            var schedule = value as ScheduleListItem;

            if (schedule == null)
            {
                return Colors.White;
            }

            var isEnabled = (schedule.DaysOfWeek & dayParameter) == dayParameter;

            if (schedule.IsEnabled)
            {
                // When alarm is enabled: light text on dark background for enabled days, darker text for disabled days
                return isEnabled ? ThemeColors.Day.EnabledText : ThemeColors.Day.DisabledText;
            }
            else
            {
                // When alarm is disabled: flip the colors - enabled days get white text (on blue), disabled days get darker text (on gray)
                // White for enabled days, darker text for disabled days
                return isEnabled ? ThemeColors.Day.EnabledText : ThemeColors.Day.DisabledText;
            }
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
