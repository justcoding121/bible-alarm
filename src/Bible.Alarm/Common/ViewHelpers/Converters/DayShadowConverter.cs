using System.Globalization;
using System.Reflection;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public class DayShadowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter == null) return null;

        DaysOfWeek daysOfWeek;

        // Get DaysOfWeek from the view model
        if (value is DaysOfWeek days)
        {
            daysOfWeek = days;
        }
        else
        {
            // Try to get DaysOfWeek property via reflection (for ScheduleViewModel)
            var daysProperty = value.GetType().GetProperty("DaysOfWeek");
            if (daysProperty != null && daysProperty.GetValue(value) is DaysOfWeek daysValue)
            {
                daysOfWeek = daysValue;
            }
            else
            {
                return null;
            }
        }

        var dayParameter = (DaysOfWeek)parameter;
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;

        // Only show shadow for selected days
        if (isDayEnabled)
        {
            return new Shadow
            {
                Brush = Color.FromArgb("#40000000"),
                Offset = new Point(0F, 2F),
                Radius = 4F,
                Opacity = 0.3F
            };
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

