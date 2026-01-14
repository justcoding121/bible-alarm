using System.ComponentModel;
using System.Globalization;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

public sealed class DayBackgroundColorConverter : IValueConverter, IMultiValueConverter, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public DayBackgroundColorConverter()
    {
        // Subscribe to theme changes when converter is instantiated
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        // Notify that the converter output has changed, causing all bindings to re-evaluate
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var theme = ThemeColors.GetCurrentTheme();
        
        if (value is null)
        {
            return ThemeColors.Day.DisabledBackground.Get(theme);
        }

        DaysOfWeek daysOfWeek;
        // Default to enabled for ScheduleViewModel
        bool isEnabled = true;

        if (value is ScheduleListItemViewModel schedule)
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
            return ThemeColors.Day.DisabledBackground.Get(theme);
        }

        var dayParameter = ParseDayParameter(parameter);
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;

        // Four combinations (theme-aware):
        if (isEnabled)
        {
            // Schedule enabled
            return isDayEnabled 
                ? ThemeColors.Day.EnabledBackground.Get(theme) 
                : ThemeColors.Day.DisabledBackground.Get(theme);
        }
        else
        {
            // Schedule disabled
            return isDayEnabled 
                ? ThemeColors.Day.DefaultBackground.Get(theme) 
                : ThemeColors.Day.AlarmDisabledDayDisabledBackground.Get(theme);
        }
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var theme = ThemeColors.GetCurrentTheme();
        
        if (values == null || values.Length < 2)
        {
            return ThemeColors.Day.DisabledBackground.Get(theme);
        }

        if (values[0] is not DaysOfWeek daysOfWeek)
        {
            return ThemeColors.Day.DisabledBackground.Get(theme);
        }

        if (values[1] is not bool isEnabled)
        {
            return ThemeColors.Day.DisabledBackground.Get(theme);
        }

        var dayParameter = ParseDayParameter(parameter);
        var isDayEnabled = (daysOfWeek & dayParameter) == dayParameter;

        // Four combinations (theme-aware):
        // 1. Schedule enabled + Day enabled: Primary color background (prominent)
        // 2. Schedule enabled + Day disabled: Neutral gray background
        // 3. Schedule disabled + Day enabled: Muted primary background (shows day selected but schedule off)
        // 4. Schedule disabled + Day disabled: More muted gray background (distinct from case 2)
        if (isEnabled)
        {
            // Schedule enabled
            return isDayEnabled 
                ? ThemeColors.Day.EnabledBackground.Get(theme) 
                : ThemeColors.Day.DisabledBackground.Get(theme);
        }
        else
        {
            // Schedule disabled
            return isDayEnabled 
                ? ThemeColors.Day.DefaultBackground.Get(theme) 
                : ThemeColors.Day.AlarmDisabledDayDisabledBackground.Get(theme);
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();

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

