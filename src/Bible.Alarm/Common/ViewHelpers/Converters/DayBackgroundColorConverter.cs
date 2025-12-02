using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class DayBackgroundColorConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Color.FromArgb("#D0D0D0");

        DaysOfWeek daysOfWeek;
        bool isEnabled = true; // Default to enabled for ScheduleViewModel

        if (value is ScheduleListItem schedule)
        {
            daysOfWeek = schedule.DaysOfWeek;
            isEnabled = schedule.IsEnabled;
        }
        else if (value is DaysOfWeek days)
        {
            daysOfWeek = days;
        }
        else if (value is ScheduleViewModel scheduleViewModel)
        {
            // Direct type check for ScheduleViewModel (no reflection needed)
            daysOfWeek = scheduleViewModel.DaysOfWeek;
            isEnabled = scheduleViewModel.IsEnabled;
        }
        else
        {
            return Color.FromArgb("#D0D0D0");
        }

        var dayParameter = ParseDayParameter(parameter);
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;

        if (isEnabled)
        {
            // When alarm is enabled: dark background for enabled days, darker for disabled days
            return isDayEnabled ? Color.FromArgb("#6A5ACD") : Color.FromArgb("#C0C0C0"); // SlateBlue for enabled, darker gray for disabled
        }
        else
        {
            // When alarm is disabled: flip the colors - enabled days get blue, disabled days get gray
            return isDayEnabled ? Color.FromArgb("#6A5ACD") : Color.FromArgb("#C0C0C0"); // SlateBlue for enabled days, gray for disabled days
        }
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return Color.FromArgb("#D0D0D0");
        
        if (values[0] is not DaysOfWeek daysOfWeek) return Color.FromArgb("#D0D0D0");
        if (values[1] is not bool isEnabled) return Color.FromArgb("#D0D0D0");

        var dayParameter = ParseDayParameter(parameter);
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;

        // When alarm is enabled: dark background for enabled days, darker for disabled days
        // When alarm is disabled: same colors - enabled days get blue, disabled days get gray
        return isDayEnabled ? Color.FromArgb("#6A5ACD") : Color.FromArgb("#C0C0C0"); // SlateBlue for enabled, gray for disabled
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static DaysOfWeek ParseDayParameter(object parameter)
    {
        if (parameter == null) return (DaysOfWeek)0;
        
        if (parameter is DaysOfWeek day)
            return day;
        
        if (parameter is string dayString && Enum.TryParse<DaysOfWeek>(dayString, out var parsedDay))
            return parsedDay;
        
        return (DaysOfWeek)0;
    }
}

