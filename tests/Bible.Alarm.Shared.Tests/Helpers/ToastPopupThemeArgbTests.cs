#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ToastPopupThemeArgbTests
{
    [Fact]
    public void Background_dark_theme_is_more_opaque_than_light_theme()
    {
        var dark = ToastPopupThemeArgb.Background(true);
        var light = ToastPopupThemeArgb.Background(false);

        Assert.True(dark.A > light.A);
    }

    [Fact]
    public void Background_dark_and_light_overlays_differ_on_blue_channel()
    {
        var dark = ToastPopupThemeArgb.Background(true);
        var light = ToastPopupThemeArgb.Background(false);

        Assert.NotEqual(dark.B, light.B);
    }
}
