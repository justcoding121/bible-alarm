#nullable enable

using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class FontServiceSizingHelpersTests
{
    [Fact]
    public void GetDeviceSizeCategory_desktop_idiom_ignores_width()
    {
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Desktop,
            FontServiceSizingHelpers.GetDeviceSizeCategory(400, DeviceIdiom.Desktop));
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Desktop,
            FontServiceSizingHelpers.GetDeviceSizeCategory(2000, DeviceIdiom.Desktop));
    }

    [Fact]
    public void GetDeviceSizeCategory_phone_idiom_under_600_is_phone()
    {
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Phone,
            FontServiceSizingHelpers.GetDeviceSizeCategory(599, DeviceIdiom.Phone));
    }

    [Fact]
    public void GetDeviceSizeCategory_phone_idiom_600_to_959_is_tablet()
    {
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Tablet,
            FontServiceSizingHelpers.GetDeviceSizeCategory(600, DeviceIdiom.Phone));
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Tablet,
            FontServiceSizingHelpers.GetDeviceSizeCategory(959, DeviceIdiom.Phone));
    }

    [Fact]
    public void GetDeviceSizeCategory_phone_idiom_960_plus_is_desktop()
    {
        Assert.Equal(
            FontServiceSizingHelpers.DeviceSizeCategory.Desktop,
            FontServiceSizingHelpers.GetDeviceSizeCategory(960, DeviceIdiom.Phone));
    }

    [Fact]
    public void GetDeviceSizeMultiplier_phone_is_one()
    {
        Assert.Equal(1.0, FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Phone, DevicePlatform.Android));
    }

    [Fact]
    public void GetDeviceSizeMultiplier_tablet_ios_android_and_default()
    {
        Assert.Equal(1.15, FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Tablet, DevicePlatform.iOS));
        Assert.Equal(1.20, FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Tablet, DevicePlatform.Android));
        Assert.Equal(1.15, FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Tablet, DevicePlatform.WinUI));
    }

    [Fact]
    public void GetDeviceSizeMultiplier_unknown_category_returns_base_multiplier()
    {
        var unknown = (FontServiceSizingHelpers.DeviceSizeCategory)99;

        Assert.Equal(1.0, FontServiceSizingHelpers.GetDeviceSizeMultiplier(unknown, DevicePlatform.Android));
    }

    [Fact]
    public void GetDeviceSizeMultiplier_desktop_winui_vs_other()
    {
        Assert.Equal(1.0, FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Desktop, DevicePlatform.WinUI));
        Assert.Equal(1.1, FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Desktop, DevicePlatform.MacCatalyst));
    }

    [Theory]
    [InlineData(599, 2.0, 1.5)]
    [InlineData(600, 2.0, 1.8)]
    [InlineData(960, 3.0, 2.2)]
    public void CalculateDensityScaleFactor_caps_by_form_factor(double widthDp, double density, double expected)
    {
        Assert.Equal(expected, FontServiceSizingHelpers.CalculateDensityScaleFactor(widthDp, density));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.9)]
    public void CalculateProgressiveAccessibilityScale_no_extra_when_scale_at_or_below_one(double scale)
    {
        Assert.Equal(1.0, FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(14.0, scale));
    }

    [Fact]
    public void CalculateProgressiveAccessibility_scale_above_one_applies_progressive_factor_by_base_size()
    {
        const double scale = 1.5;
        Assert.Equal(1.5, FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(10.0, scale));
        Assert.True(FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(14.0, scale) > 1.0);
        Assert.True(FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(18.0, scale) <
                     FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(14.0, scale));
        Assert.True(FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(35.0, scale) >= 1.04);
    }

    [Fact]
    public void GetAlarmMaxSizes_phone_platform_ios()
    {
        var tuple = FontServiceSizingHelpers.GetAlarmMaxSizes(true, 1.0, DevicePlatform.iOS, 1.0);
        Assert.True(tuple.MaxTime > 0);
        Assert.True(tuple.MaxMeridian > 0);
        Assert.True(tuple.MaxBellIcon > 0);
    }

    [Fact]
    public void GetAlarmMaxSizes_not_phone_android()
    {
        var tuple = FontServiceSizingHelpers.GetAlarmMaxSizes(false, 1.0, DevicePlatform.Android, 1.0);
        Assert.True(tuple.MaxTime > 0);
        Assert.True(tuple.MaxMeridian > 0);
        Assert.True(tuple.MaxBellIcon > 100);
    }

    [Fact]
    public void GetAlarmMaxSizes_unknown_platform_uses_android_alarm_defaults()
    {
        var a = FontServiceSizingHelpers.GetAlarmMaxSizes(true, 1.2, DevicePlatform.MacCatalyst, 1.0);
        var b = FontServiceSizingHelpers.GetAlarmMaxSizes(true, 1.2, DevicePlatform.Android, 1.0);
        Assert.Equal(a.MaxTime, b.MaxTime);
    }

    [Fact]
    public void GetAlarmMaxSizes_uses_windows_defaults_for_winui()
    {
        var t = FontServiceSizingHelpers.GetAlarmMaxSizes(isPhone: false, 1.0, DevicePlatform.WinUI, deviceSizeMultiplier: 1.0);

        Assert.True(t.MaxTime > 0);
        Assert.True(t.MaxMeridian > 0);
        Assert.True(t.MaxBellIcon > 100);
    }
}
