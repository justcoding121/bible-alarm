#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.UI;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class FontServiceTests : IDisposable
{
    private static DisplayInfo Display(double width, double height, double density) =>
        new(width, height, (float)density, DisplayOrientation.Portrait, DisplayRotation.Rotation0);

    public FontServiceTests()
    {
        ClearFontServiceTestHooks();
        FontService.SkipDeviceDisplaySubscriptionForTests = true;
    }

    public void Dispose() => ClearFontServiceTestHooks();

    private static void ClearFontServiceTestHooks()
    {
        FontService.MainDisplayInfoOverrideForTests = null;
        FontService.DevicePlatformOverrideForTests = null;
        FontService.DeviceIdiomOverrideForTests = null;
        FontService.SkipDeviceDisplaySubscriptionForTests = false;
    }

    private sealed class FakeAccessibilityFontScaleService : IAccessibilityFontScaleService
    {
        public double FontScale { get; set; } = 1.0;

        public event EventHandler<double>? FontScaleChanged;

        public void RaiseFontScaleChanged(double newScale)
        {
            FontScale = newScale;
            FontScaleChanged?.Invoke(this, newScale);
        }
    }

    [Fact]
    public void HasValidDisplayInfo_zero_width_returns_false()
    {
        Assert.False(FontService.HasValidDisplayInfo(Display(0, 100, 2)));
    }

    [Fact]
    public void HasValidDisplayInfo_zero_height_returns_false()
    {
        Assert.False(FontService.HasValidDisplayInfo(Display(100, 0, 2)));
    }

    [Fact]
    public void HasValidDisplayInfo_zero_density_returns_false()
    {
        Assert.False(FontService.HasValidDisplayInfo(Display(100, 100, 0)));
    }

    [Fact]
    public void HasValidDisplayInfo_positive_extents_returns_true()
    {
        Assert.True(FontService.HasValidDisplayInfo(Display(100, 200, 2)));
    }

    [Fact]
    public void SetWindowsDesktopFallbackFontSizes_multiplier_one_scale_one_matches_fluent_base_sizes()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        using var sut = new FontService(acc);
        sut.SetWindowsDesktopFallbackFontSizes(deviceSizeMultiplier: 1.0);

        Assert.Equal(PlatformFontDefaults.Windows.StandardSize, sut.StandardFontSize, precision: 6);
        Assert.Equal(PlatformFontDefaults.Windows.HeaderSize, sut.HeaderFontSize, precision: 6);
    }

    [Fact]
    public void SetWindowsDesktopFallbackFontSizes_applies_progressive_accessibility_scaling()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        using var sut = new FontService(acc);
        sut.SetWindowsDesktopFallbackFontSizes(1.0);
        var baselineHeader = sut.HeaderFontSize;

        acc.FontScale = 2.0;
        sut.SetWindowsDesktopFallbackFontSizes(1.0);

        var expectedFactor = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(
            PlatformFontDefaults.Windows.HeaderSize,
            accessibilityScale: 2.0);
        Assert.Equal(PlatformFontDefaults.Windows.HeaderSize * expectedFactor, sut.HeaderFontSize, precision: 6);
        Assert.True(sut.HeaderFontSize > baselineHeader);
    }

    [Fact]
    public void SetOtherPlatformFallbackFontSizes_Android_uses_android_defaults_at_scale_one()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        using var sut = new FontService(acc);
        sut.SetOtherPlatformFallbackFontSizes(isAndroid: true, deviceSizeMultiplier: 1.0);

        Assert.Equal(PlatformFontDefaults.Android.StandardSize, sut.StandardFontSize, precision: 6);
        Assert.Equal(PlatformFontDefaults.Android.AlarmTimeSize, sut.AlarmTimeFontSize, precision: 6);
    }

    [Fact]
    public void SetOtherPlatformFallbackFontSizes_iOS_uses_ios_defaults_at_scale_one()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        using var sut = new FontService(acc);
        sut.SetOtherPlatformFallbackFontSizes(isAndroid: false, deviceSizeMultiplier: 1.0);

        Assert.Equal(PlatformFontDefaults.iOS.StandardSize, sut.StandardFontSize, precision: 6);
        Assert.Equal(PlatformFontDefaults.iOS.AlarmTimeSize, sut.AlarmTimeFontSize, precision: 6);
    }

    [Fact]
    public void SetOtherPlatformFallbackFontSizes_alarm_sizes_respect_math_min_caps_at_high_accessibility()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 4.0 };
        using var sut = new FontService(acc);

        sut.SetOtherPlatformFallbackFontSizes(isAndroid: true, deviceSizeMultiplier: 1.0);
        var b = PlatformFontDefaults.Android;

        double baseAlarmTime = b.AlarmTimeSize;
        double baseMeridian = b.AlarmMeridianSize;
        const double baseBell = 80.0;

        var timeScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseAlarmTime, 4.0);
        var clampedTimeScale = FontServiceAlarmTimeProgressiveScaleClamp.Apply(baseAlarmTime, 4.0, timeScale);
        var meridianScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseMeridian, 4.0);
        var bellScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseBell, 4.0);

        var maxProgressive = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(30.0, 4.0);
        var maxScaleFactor = 1.0 + ((maxProgressive - 1.0) * 0.5);
        var fallbackMaxTime = baseAlarmTime * 2.5 * maxScaleFactor;
        var fallbackMaxMeridian = baseMeridian * 2.0 * maxScaleFactor;

        var uncappedTime = baseAlarmTime * clampedTimeScale;
        Assert.Equal(Math.Min(uncappedTime, fallbackMaxTime), sut.AlarmTimeFontSize, precision: 6);
        Assert.Equal(Math.Min(baseMeridian * meridianScale, fallbackMaxMeridian), sut.AlarmMeridianFontSize, precision: 6);
        Assert.Equal(
            Math.Min(baseBell * bellScale, 100.0 * maxScaleFactor),
            sut.AlarmBellIconFontSize,
            precision: 6);
    }

    [Fact]
    public void SetScaledFontSizes_uses_width_dp_and_platform_settings()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        FontService.MainDisplayInfoOverrideForTests = () => Display(1600, 900, 2);
        FontService.DevicePlatformOverrideForTests = () => DevicePlatform.WinUI;
        FontService.DeviceIdiomOverrideForTests = () => DeviceIdiom.Phone;

        using var sut = new FontService(acc);

        // 1600x900 @ 2.0 density => 800dp wide => tablet category for phone idiom, WinUI tablet multiplier 1.15
        var display = Display(1600, 900, 2);
        sut.SetScaledFontSizes(display);

        var defaults = PlatformFontDefaults.Windows;
        double deviceSizeMultiplier = FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FontServiceSizingHelpers.DeviceSizeCategory.Tablet,
            DevicePlatform.WinUI);

        double baseStandard = defaults.StandardSize * deviceSizeMultiplier;
        var progressive = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseStandard, 1.0);
        var expectedStandard = Math.Min(
            baseStandard * progressive,
            baseStandard * 1.5 * Math.Max(1.0, 1.0));

        Assert.Equal(expectedStandard, sut.StandardFontSize, precision: 6);

        // Header changes in SetScaledFontSizes; ButtonFontSize is only synced in Recalculate.
        sut.Recalculate();
        Assert.Equal(sut.HeaderFontSize - 1.0, sut.ButtonFontSize, precision: 6);
    }

    [Fact]
    public void SetAlarmFontSizes_compact_phone_width_uses_lower_max_cap_than_wider_layout()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 3.0 };
        using var sut = new FontService(acc);

        sut.SetAlarmFontSizes(3.0, widthDp: 400, DevicePlatform.WinUI, deviceSizeMultiplier: 1.0);
        var phoneAlarmTime = sut.AlarmTimeFontSize;

        sut.SetAlarmFontSizes(3.0, widthDp: 1200, DevicePlatform.WinUI, deviceSizeMultiplier: 1.0);
        var wideAlarmTime = sut.AlarmTimeFontSize;

        Assert.True(PhoneLayoutWidthClassification.IsCompactPhoneWidth(400));
        Assert.False(PhoneLayoutWidthClassification.IsCompactPhoneWidth(1200));
        Assert.True(wideAlarmTime >= phoneAlarmTime);
    }

    [Fact]
    public void Recalculate_invalid_display_uses_fallback_font_sizes_branch()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        FontService.MainDisplayInfoOverrideForTests = () => Display(0, 1080, 2);
        FontService.DevicePlatformOverrideForTests = () => DevicePlatform.WinUI;
        FontService.DeviceIdiomOverrideForTests = () => DeviceIdiom.Desktop;

        using var sut = new FontService(acc);
        var mult = FontServiceSizingHelpers.GetDeviceSizeMultiplier(
            FallbackFontDeviceCategoryResolver.Resolve(DeviceIdiom.Desktop),
            DevicePlatform.WinUI);
        Assert.Equal(1.0, mult);

        // Invalid metrics => SetFallbackFontSizes => Windows desktop Fluent defaults at multiplier 1.0
        Assert.Equal(PlatformFontDefaults.Windows.StandardSize, sut.StandardFontSize, precision: 6);
        Assert.Equal(sut.HeaderFontSize - 1.0, sut.ButtonFontSize, precision: 6);
    }

    [Fact]
    public void Recalculate_valid_display_uses_scaled_font_sizes()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        FontService.MainDisplayInfoOverrideForTests = () => Display(1600, 900, 2);
        FontService.DevicePlatformOverrideForTests = () => DevicePlatform.WinUI;
        FontService.DeviceIdiomOverrideForTests = () => DeviceIdiom.Phone;

        using var sut = new FontService(acc);
        sut.SetScaledFontSizes(Display(1600, 900, 2));
        var expectedStandard = sut.StandardFontSize;

        sut.Recalculate();
        Assert.Equal(expectedStandard, sut.StandardFontSize, precision: 6);
    }

    [Fact]
    public void SetFallbackFontSizes_windows_desktop_branch_uses_windows_fluent_defaults()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        FontService.DevicePlatformOverrideForTests = () => DevicePlatform.WinUI;
        FontService.DeviceIdiomOverrideForTests = () => DeviceIdiom.Desktop;

        using var sut = new FontService(acc);
        sut.SetFallbackFontSizes(DevicePlatform.WinUI, isAndroid: false);

        Assert.Equal(PlatformFontDefaults.Windows.StandardSize, sut.StandardFontSize, precision: 6);
    }

    [Fact]
    public void SetFallbackFontSizes_non_windows_phone_idiom_uses_other_platform_branch()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        FontService.DevicePlatformOverrideForTests = () => DevicePlatform.Android;
        FontService.DeviceIdiomOverrideForTests = () => DeviceIdiom.Phone;

        using var sut = new FontService(acc);
        sut.SetFallbackFontSizes(DevicePlatform.Android, isAndroid: true);

        Assert.Equal(PlatformFontDefaults.Android.StandardSize, sut.StandardFontSize, precision: 6);
    }

    [Fact]
    public void Dispose_second_call_is_noop_and_font_scale_events_stop_updating()
    {
        var acc = new FakeAccessibilityFontScaleService { FontScale = 1.0 };
        FontService.MainDisplayInfoOverrideForTests = () => Display(800, 600, 2);
        FontService.DevicePlatformOverrideForTests = () => DevicePlatform.WinUI;
        FontService.DeviceIdiomOverrideForTests = () => DeviceIdiom.Phone;

        var sut = new FontService(acc);
        var headerAfterInit = sut.HeaderFontSize;

        acc.FontScale = 2.0;
        acc.RaiseFontScaleChanged(2.0);
        var headerAfterLiveEvent = sut.HeaderFontSize;
        Assert.NotEqual(headerAfterInit, headerAfterLiveEvent);

        sut.Dispose();
        sut.Dispose();

        acc.FontScale = 1.0;
        acc.RaiseFontScaleChanged(1.0);
        Assert.Equal(headerAfterLiveEvent, sut.HeaderFontSize, precision: 6);
    }
}
