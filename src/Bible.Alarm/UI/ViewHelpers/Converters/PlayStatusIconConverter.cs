using System.Globalization;
using Bible.Alarm.UI.Views;

namespace Bible.Alarm.UI.ViewHelpers.Converters;

public class PlayStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(bool)value) return GlyphNames.Play;

        return GlyphNames.Stop;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}