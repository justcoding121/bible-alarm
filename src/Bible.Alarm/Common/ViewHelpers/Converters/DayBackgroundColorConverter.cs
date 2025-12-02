using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class DayBackgroundColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Color.FromArgb("#D0D0D0");

        var schedule = value as ScheduleListItem;
        if (schedule == null) return Color.FromArgb("#D0D0D0");

        var isDayEnabled = (schedule.DaysOfWeek & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;

        if (schedule.IsEnabled)
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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

