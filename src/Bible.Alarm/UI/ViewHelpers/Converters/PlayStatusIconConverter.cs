using FontNameResources;
using System.Globalization;

namespace Bible.Alarm.UI.Views.Converters;

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