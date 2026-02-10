#nullable enable
using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Multi-binding converter that returns true if ANY of the bound boolean values is true (OR logic).
/// </summary>
public class MultiBoolOrConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value is bool boolValue && boolValue)
            {
                var result = true;
                if (parameter is "Inverse" or "Negate")
                {
                    result = false;
                }
                return result;
            }
        }

        var defaultValue = false;
        if (parameter is "Inverse" or "Negate")
        {
            defaultValue = true;
        }
        return defaultValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
