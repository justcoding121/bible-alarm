#nullable enable

using System.Globalization;
using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Bible.Alarm.Tests;

public sealed class IsEnabledColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_enabled_with_color_parameter_returns_parameter()
    {
        var sut = new IsEnabledColorConverter();
        var accent = Color.FromArgb("#FF8866");

        Assert.Equal(accent, sut.Convert(true, typeof(Color), accent, Cul));
    }

    [Fact]
    public void Convert_parses_bool_from_string()
    {
        var sut = new IsEnabledColorConverter();
        var accent = Color.FromArgb("#112233");

        Assert.Equal(accent, sut.Convert("true", typeof(Color), accent, Cul));
    }

    [Fact]
    public void Convert_disabled_uses_disabled_resource_or_theme_fallback()
    {
        var sut = new IsEnabledColorConverter();

        var result = (Color)sut.Convert(false, typeof(Color), null!, Cul);

        if (Application.Current?.Resources.TryGetValue("DisabledTextColor", out var obj) is true &&
            obj is Color fromResource)
        {
            Assert.Equal(fromResource, result);
        }
        else
        {
            var theme = ThemeColors.GetCurrentTheme();
            Assert.Equal(ThemeColors.Fallback.ConverterMutedText.Get(theme), result);
        }
    }

    [Fact]
    public void Convert_enabled_without_parameter_uses_text_primary_or_os_contrast_fallback()
    {
        var sut = new IsEnabledColorConverter();

        var result = (Color)sut.Convert(true, typeof(Color), null!, Cul);

        if (Application.Current?.Resources.TryGetValue("TextPrimaryColor", out var obj) is true &&
            obj is Color fromResource)
        {
            Assert.Equal(fromResource, result);
        }
        else
        {
            var expected = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new IsEnabledColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Gray, typeof(Color), null!, Cul));
    }
}
