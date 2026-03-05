#nullable enable

using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Converts a TimeSpan to the culture-aware AM/PM designator string.
/// Used on iOS to display the meridian as a separate Label, avoiding the
/// UIDatePicker triangle rendering bug when the TimePicker format includes "tt".
/// </summary>
public sealed class TimeToMeridianConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("tt", CultureInfo.CurrentCulture);
        }

        return DateTime.Today.ToString("tt", CultureInfo.CurrentCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
