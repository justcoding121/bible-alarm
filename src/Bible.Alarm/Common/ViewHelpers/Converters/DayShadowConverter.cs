using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public sealed class DayShadowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter == null)
        {
            return null;
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return null;
        }

        DaysOfWeek daysOfWeek;

        // Get DaysOfWeek from the view model
        if (value is DaysOfWeek days)
        {
            daysOfWeek = days;
        }
        else if (value is ScheduleViewModel scheduleViewModel)
        {
            // Direct type check for ScheduleViewModel (no reflection needed)
            daysOfWeek = scheduleViewModel.DaysOfWeek;
        }
        else
        {
            return null;
        }

        var dayParameter = ParseDayParameter(parameter);
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;

        // Only show shadow for selected days
        if (isDayEnabled)
        {
            return new Shadow
            {
                Brush = ThemeColors.Animation.Shadow,
                Offset = new Point(0F, 2F),
                Radius = 4F,
                Opacity = 0.3F
            };
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

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
}

