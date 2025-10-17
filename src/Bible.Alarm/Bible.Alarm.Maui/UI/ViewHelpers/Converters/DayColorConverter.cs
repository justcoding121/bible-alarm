using Bible.Alarm.Models;
using Bible.Alarm.ViewModels;
using System;
using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.UI.Views.Converters
{
    public class DayColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return Colors.LightGray;
            }

            if (value is DaysOfWeek)
            {
                var isEnabled = ((DaysOfWeek)value & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;
                return isEnabled ? Colors.SlateBlue : Colors.LightGray;
            }
            else
            {
                var schedule = value as ScheduleListItem;

                var isEnabled = ((DaysOfWeek)schedule.DaysOfWeek & (DaysOfWeek)parameter) == (DaysOfWeek)parameter;

                if (schedule.IsEnabled)
                {
                    return isEnabled ? Colors.SlateBlue : Colors.LightGray;
                }
                else
                {
                    return isEnabled ? Colors.LightGray : Colors.WhiteSmoke;
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
