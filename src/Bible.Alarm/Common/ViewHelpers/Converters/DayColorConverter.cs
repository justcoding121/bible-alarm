using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public sealed class DayColorConverter : IValueConverter
{
    private static DaysOfWeek ParseDayParameter(object parameter)
    {
        return parameter switch
        {
            null => 0,
            DaysOfWeek day => day,
            string dayString when Enum.TryParse<DaysOfWeek>(dayString, out var parsedDay) => parsedDay,
            _ => 0
        };
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
            var schedule = value as ScheduleListItemViewModel;

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

            // When alarm is disabled: flip the colors - enabled days get white text (on blue), disabled days get darker text (on gray)
            // White for enabled days, darker text for disabled days
            return isEnabled ? ThemeColors.Day.EnabledText : ThemeColors.Day.DisabledText;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
