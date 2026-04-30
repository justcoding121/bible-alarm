using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public sealed class DayColorConverter : IValueConverter, IMultiValueConverter
{
    private static WeekDays ParseDayParameter(object parameter)
    {
        return parameter switch
        {
            null => 0,
            WeekDays day => day,
            string dayString when Enum.TryParse<WeekDays>(dayString, out var parsedDay) => parsedDay,
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

        var theme = ThemeColors.GetCurrentTheme();

        if (value is WeekDays dayMask)
        {
            var isDayEnabled = (dayMask & dayParameter) == dayParameter;
            return isDayEnabled 
                ? ThemeColors.Day.EnabledText.Get(theme) 
                : ThemeColors.Day.CalendarMutedText.Get(theme);
        }

        if (value is ScheduleListItemViewModel schedule)
        {
            var isDayEnabled = (schedule.DaysOfWeek & dayParameter) == dayParameter;

            if (schedule.IsEnabled)
            {
                // Schedule enabled
                return isDayEnabled 
                    ? ThemeColors.Day.EnabledText.Get(theme) 
                    : ThemeColors.Day.CalendarMutedText.Get(theme);
            }

            // Schedule disabled - all days use disabled text color
            return ThemeColors.Day.CalendarMutedText.Get(theme);
        }

        return Colors.White;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return Colors.White;
        }

        if (values[0] is not WeekDays daysOfWeek)
        {
            return Colors.White;
        }

        if (values[1] is not bool isEnabled)
        {
            return Colors.White;
        }

        var dayParameter = ParseDayParameter(parameter);
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;
        var theme = ThemeColors.GetCurrentTheme();

        // Four combinations (theme-aware):
        // 1. Schedule enabled + Day enabled: White text on primary background (high contrast)
        // 2. Schedule enabled + Day disabled: Disabled text on gray background
        // 3. Schedule disabled + Day enabled: Disabled text on muted background
        // 4. Schedule disabled + Day disabled: Disabled text on gray background
        if (isEnabled)
        {
            // Schedule enabled
            return isDayEnabled 
                ? ThemeColors.Day.EnabledText.Get(theme) 
                : ThemeColors.Day.CalendarMutedText.Get(theme);
        }
        else
        {
            // Schedule disabled - all days use disabled text color (theme-aware)
            return ThemeColors.Day.CalendarMutedText.Get(theme);
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
