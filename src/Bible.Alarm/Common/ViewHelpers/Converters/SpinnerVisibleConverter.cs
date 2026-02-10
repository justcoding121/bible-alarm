#nullable enable
using System.Globalization;

namespace Bible.Alarm.Common.ViewHelpers.Converters;

/// <summary>
/// Multi-value converter for spinner visibility: show spinner only when busy/progress and NOT in error state.
/// With 3 bools (ShowProgress, IsBusy, HasFetchError): returns (ShowProgress || IsBusy) &amp;&amp; !HasFetchError.
/// With 2 bools (IsVisible, HasFetchError): returns IsVisible &amp;&amp; !HasFetchError (for opacity 1.0/0.0 when targetType is double).
/// </summary>
public sealed class SpinnerVisibleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return targetType == typeof(double) ? 0.0 : false;

        bool show = false;
        if (values.Length >= 3)
        {
            var a = values[0] is bool b0 && b0;
            var b = values[1] is bool b1 && b1;
            var err = values[2] is bool b2 && b2;
            show = (a || b) && !err;
        }
        else
        {
            var visible = values[0] is bool v0 && v0;
            var err = values[1] is bool e0 && e0;
            show = visible && !err;
        }

        if (targetType == typeof(double))
            return show ? 1.0 : 0.0;
        return show;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
