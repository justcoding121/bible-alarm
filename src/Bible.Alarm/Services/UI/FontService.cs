#nullable enable
using System.ComponentModel;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Service for managing scalable font sizes based on device metrics and OS accessibility settings.
/// Uses screen width in density-independent pixels (dp) to determine device category (phone/tablet/desktop),
/// then applies platform-specific defaults, device size multipliers, density scaling, and OS accessibility font scale.
/// 
/// Device size scaling per industry standards:
/// - Phones: 1.0x (base size)
/// - Tablets: 1.15-1.20x (larger for better readability at viewing distance)
/// - Desktop: 1.0-1.1x (consistent desktop sizes)
/// 
/// Automatically recalculates when screen size/orientation changes or OS font scale changes.
/// </summary>
public sealed class FontService : IFontService, INotifyPropertyChanged, IDisposable
{
    private bool isDisposed;
    private double standardFontSize;
    private double headerFontSize;
    private double buttonFontSize;
    private double smallFontSize;
    private double smallMediumFontSize;
    private double mediumFontSize;
    private double largeFontSize;
    private double titleFontSize;
    private double alarmTimeFontSize;
    private double alarmMeridianFontSize;
    private double alarmBellIconFontSize;
    private double iconSmallFontSize;
    private double iconStandardFontSize;
    private double iconLargeFontSize;
    private double iconSmallContainerSize;
    private double iconStandardContainerSize;
    private double iconLargeContainerSize;
    private double contentWidthSmall;
    private double contentWidthMedium;
    private double contentWidthLarge;
    private double spinnerContainerSize;
    private double spinnerFontSize;

    private readonly IAccessibilityFontScaleService accessibilityFontScaleService;

    private void SetSpinnerSizes()
    {
        if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            spinnerContainerSize = Math.Max(56.0, iconStandardContainerSize);
            spinnerFontSize = Math.Max(24.0, iconStandardFontSize);
        }
        else
        {
            spinnerContainerSize = iconStandardContainerSize;
            spinnerFontSize = iconStandardFontSize;
        }
    }

    public FontService(IAccessibilityFontScaleService accessibilityFontScaleService)
    {
        this.accessibilityFontScaleService = accessibilityFontScaleService;

        // Listen for screen size/orientation changes
        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;

        // Listen for OS accessibility font scale changes
        accessibilityFontScaleService.FontScaleChanged += OnFontScaleChanged;

        // Initial calculation
        Recalculate();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => Recalculate();

    private void OnFontScaleChanged(object? sender, double newScale) => Recalculate();

    private void Recalculate()
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
        bool hasValidDisplayInfo = HasValidDisplayInfo(mainDisplayInfo);

        var platform = DeviceInfo.Platform;
        bool isAndroid = platform == DevicePlatform.Android;
        if (!hasValidDisplayInfo)
        {
            SetFallbackFontSizes(platform, isAndroid);
        }
        else
        {
            SetScaledFontSizes(mainDisplayInfo, isAndroid);
        }

        buttonFontSize = headerFontSize - 1.0;
        SetSpinnerSizes();
        RaiseAllPropertiesChanged();
    }

    private static bool HasValidDisplayInfo(DisplayInfo displayInfo)
    {
        return displayInfo.Width > 0 &&
               displayInfo.Height > 0 &&
               displayInfo.Density > 0;
    }

    private void SetFallbackFontSizes(DevicePlatform platform, bool isAndroid)
    {
        var deviceIdiom = DeviceInfo.Idiom;
        
        // Determine device size category for fallback (use screen width if available, otherwise use idiom)
        // Default to phone if we can't determine
        var deviceSizeCategory = deviceIdiom == DeviceIdiom.Desktop
            ? FontServiceSizingHelpers.DeviceSizeCategory.Desktop
            : FontServiceSizingHelpers.DeviceSizeCategory.Phone;
        double deviceSizeMultiplier = FontServiceSizingHelpers.GetDeviceSizeMultiplier(deviceSizeCategory, platform);

        if (platform == DevicePlatform.WinUI || deviceIdiom == DeviceIdiom.Desktop)
        {
            SetWindowsDesktopFallbackFontSizes(deviceSizeMultiplier);
        }
        else
        {
            SetOtherPlatformFallbackFontSizes(isAndroid, deviceSizeMultiplier);
        }
    }

    private void SetWindowsDesktopFallbackFontSizes(double deviceSizeMultiplier)
    {
        // Apply progressive accessibility scaling to fallback sizes
        // Use Windows-specific defaults per Fluent Design System
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        var defaults = PlatformFontDefaults.Windows;
        // Apply device size multiplier (desktop typically uses 1.0x, but tablets may use larger)
        double BaseStandardSize = defaults.StandardSize * deviceSizeMultiplier;
        double BaseHeaderSize = defaults.HeaderSize * deviceSizeMultiplier;
        double BaseSmallSize = defaults.SmallSize * deviceSizeMultiplier;
        double BaseSmallMediumSize = defaults.SmallMediumSize * deviceSizeMultiplier;
        double BaseMediumSize = defaults.MediumSize * deviceSizeMultiplier;
        double BaseLargeSize = defaults.LargeSize * deviceSizeMultiplier;
        double BaseTitleSize = defaults.TitleSize * deviceSizeMultiplier;

        standardFontSize = BaseStandardSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        headerFontSize = BaseHeaderSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        smallFontSize = BaseSmallSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        smallMediumFontSize = BaseSmallMediumSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        mediumFontSize = BaseMediumSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        largeFontSize = BaseLargeSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        titleFontSize = BaseTitleSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Alarm fonts use platform-specific defaults per industry standards
        var windowsDefaults = PlatformFontDefaults.Windows;
        double BaseAlarmTimeSize = windowsDefaults.AlarmTimeSize * deviceSizeMultiplier;
        double BaseAlarmMeridianSize = windowsDefaults.AlarmMeridianSize * deviceSizeMultiplier;
        const double BaseAlarmBellIconSize = 80.0;

        // Apply progressive accessibility scaling with moderate boost for alarm time
        // Maintain progressive scaling hierarchy: larger fonts should scale less than smaller fonts
        double alarmTimeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseAlarmTimeSize, accessibilityScale);
        // If alarm time is very large (>20pt), scale it at least as much as headline, but not more
        if (BaseAlarmTimeSize > 20.0 && accessibilityScale > 1.0)
        {
            // Calculate what headline (20pt) would scale to - this is our reference
            double headlineProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(20.0, accessibilityScale);
            // Use the higher of: current minimal scale OR headline scale
            alarmTimeProgressiveScale = Math.Max(alarmTimeProgressiveScale, headlineProgressiveScale);
            // Cap it so it never exceeds headline scaling by more than 10% (maintains hierarchy)
            alarmTimeProgressiveScale = Math.Min(alarmTimeProgressiveScale, headlineProgressiveScale * 1.1);
        }
        
        alarmTimeFontSize = BaseAlarmTimeSize * alarmTimeProgressiveScale;
        alarmMeridianFontSize = BaseAlarmMeridianSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseAlarmMeridianSize, accessibilityScale);
        alarmBellIconFontSize = BaseAlarmBellIconSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Icon font sizes
        const double BaseIconSmallSize = 14.0;
        const double BaseIconStandardSize = 20.0;
        const double BaseIconLargeSize = 28.0;

        iconSmallFontSize = BaseIconSmallSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        iconStandardFontSize = BaseIconStandardSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        iconLargeFontSize = BaseIconLargeSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        // Icon container sizes scale proportionally with their icons
        const double BaseIconSmallContainerSize = 32.0;
        const double BaseIconStandardContainerSize = 48.0;
        const double BaseIconLargeContainerSize = 56.0;

        double iconSmallProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        double iconStandardProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        double iconLargeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        iconSmallContainerSize = BaseIconSmallContainerSize * iconSmallProgressiveScale;
        iconStandardContainerSize = BaseIconStandardContainerSize * iconStandardProgressiveScale;
        iconLargeContainerSize = BaseIconLargeContainerSize * iconLargeProgressiveScale;

        // Content widths scale conservatively
        double contentWidthProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(14.0, accessibilityScale);
        contentWidthSmall = 200.0 * contentWidthProgressiveScale;
        contentWidthMedium = 240.0 * contentWidthProgressiveScale;
        contentWidthLarge = 280.0 * contentWidthProgressiveScale;

        Log.Logger.Debug("Using Windows desktop fallback font sizes (Body: {Body}pt, Header: {Header}pt) with device size multiplier {DeviceSizeMultiplier:F2} and progressive accessibility scale {AccessibilityScale:F2}",
            BaseStandardSize, BaseHeaderSize, deviceSizeMultiplier, accessibilityScale);
    }

    private void SetOtherPlatformFallbackFontSizes(bool isAndroid, double deviceSizeMultiplier)
    {
        // Apply progressive accessibility scaling to fallback sizes
        // Use platform-specific defaults: iOS or Android based on industry standards
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        var defaults = isAndroid ? PlatformFontDefaults.Android : PlatformFontDefaults.iOS;
        // Apply device size multiplier (tablets get larger fonts per industry standards)
        double BaseStandardSize = defaults.StandardSize * deviceSizeMultiplier;
        double BaseHeaderSize = defaults.HeaderSize * deviceSizeMultiplier;
        double BaseSmallSize = defaults.SmallSize * deviceSizeMultiplier;
        double BaseSmallMediumSize = defaults.SmallMediumSize * deviceSizeMultiplier;
        double BaseMediumSize = defaults.MediumSize * deviceSizeMultiplier;
        double BaseLargeSize = defaults.LargeSize * deviceSizeMultiplier;
        double BaseTitleSize = defaults.TitleSize * deviceSizeMultiplier;

        standardFontSize = BaseStandardSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        headerFontSize = BaseHeaderSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        smallFontSize = BaseSmallSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        smallMediumFontSize = BaseSmallMediumSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        mediumFontSize = BaseMediumSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        largeFontSize = BaseLargeSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        titleFontSize = BaseTitleSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Icon font sizes
        const double BaseIconSmallSize = 14.0;
        const double BaseIconStandardSize = 20.0;
        const double BaseIconLargeSize = 28.0;

        iconSmallFontSize = BaseIconSmallSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        iconStandardFontSize = BaseIconStandardSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        iconLargeFontSize = BaseIconLargeSize * FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        // Icon container sizes scale proportionally with their icons
        const double BaseIconSmallContainerSize = 32.0;
        const double BaseIconStandardContainerSize = 48.0;
        const double BaseIconLargeContainerSize = 56.0;

        double iconSmallProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        double iconStandardProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        double iconLargeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        iconSmallContainerSize = BaseIconSmallContainerSize * iconSmallProgressiveScale;
        iconStandardContainerSize = BaseIconStandardContainerSize * iconStandardProgressiveScale;
        iconLargeContainerSize = BaseIconLargeContainerSize * iconLargeProgressiveScale;

        // Content widths scale conservatively
        double contentWidthProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(14.0, accessibilityScale);
        contentWidthSmall = 200.0 * contentWidthProgressiveScale;
        contentWidthMedium = 240.0 * contentWidthProgressiveScale;
        contentWidthLarge = 280.0 * contentWidthProgressiveScale;

        // Alarm fonts use platform-specific defaults per industry standards
        var platformDefaults = isAndroid ? PlatformFontDefaults.Android : PlatformFontDefaults.iOS;
        // No reduction for alarm time - should be prominent and large
        double baseAlarmTimeSize = platformDefaults.AlarmTimeSize * deviceSizeMultiplier;
        double baseAlarmMeridianSize = platformDefaults.AlarmMeridianSize * deviceSizeMultiplier;
        const double BaseAlarmBellIconSize = 80.0;

        // Apply progressive accessibility scaling with moderate boost for alarm time
        // Maintain progressive scaling hierarchy: larger fonts should scale less than smaller fonts
        double alarmTimeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseAlarmTimeSize, accessibilityScale);
        // If alarm time is very large (>20pt), scale it at least as much as headline, but not more
        if (baseAlarmTimeSize > 20.0 && accessibilityScale > 1.0)
        {
            // Calculate what headline (20pt) would scale to - this is our reference
            double headlineProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(20.0, accessibilityScale);
            // Use the higher of: current minimal scale OR headline scale
            alarmTimeProgressiveScale = Math.Max(alarmTimeProgressiveScale, headlineProgressiveScale);
            // Cap it so it never exceeds headline scaling by more than 10% (maintains hierarchy)
            alarmTimeProgressiveScale = Math.Min(alarmTimeProgressiveScale, headlineProgressiveScale * 1.1);
        }
        
        double alarmMeridianProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseAlarmMeridianSize, accessibilityScale);
        double alarmBellIconProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Apply max caps for alarm fonts (allow up to 2.5x for accessibility)
        double maxProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(30.0, accessibilityScale);
        double maxScaleFactor = 1.0 + ((maxProgressiveScale - 1.0) * 0.5);
        double fallbackMaxTime = baseAlarmTimeSize * 2.5 * maxScaleFactor;
        double fallbackMaxMeridian = baseAlarmMeridianSize * 2.0 * maxScaleFactor;

        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * alarmTimeProgressiveScale, fallbackMaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * alarmMeridianProgressiveScale, fallbackMaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * alarmBellIconProgressiveScale, 100.0 * maxScaleFactor);

        Log.Logger.Warning("Invalid display info detected on {Platform} platform, using fallback font sizes (Body: {Body}pt, Header: {Header}pt) with device size multiplier {DeviceSizeMultiplier:F2} and progressive accessibility scale {AccessibilityScale:F2}",
            isAndroid ? "Android" : "iOS", BaseStandardSize, BaseHeaderSize, deviceSizeMultiplier, accessibilityScale);
    }

    private void SetScaledFontSizes(DisplayInfo mainDisplayInfo, bool isAndroid)
    {
        double density = mainDisplayInfo.Density;
        double widthDp = mainDisplayInfo.Width / density;

        // Get the OS accessibility font scale (1.0 = normal, >1.0 = larger for accessibility)
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        var platform = DeviceInfo.Platform;
        var deviceIdiom = DeviceInfo.Idiom;
        
        // Determine device size category and multiplier
        var deviceSizeCategory = FontServiceSizingHelpers.GetDeviceSizeCategory(widthDp, deviceIdiom);
        double deviceSizeMultiplier = FontServiceSizingHelpers.GetDeviceSizeMultiplier(deviceSizeCategory, platform);
        
        double densityScale = FontServiceSizingHelpers.CalculateDensityScaleFactor(widthDp, density);
        SetStandardFontSizes(accessibilityScale, platform, deviceSizeMultiplier);
        SetAlarmFontSizes(accessibilityScale, widthDp, platform, deviceSizeMultiplier);

        Log.Logger.Debug("Font sizes updated with density scale {DensityScale:F2}, accessibility scale {AccessibilityScale:F2}, device size multiplier {DeviceSizeMultiplier:F2} for platform {Platform} ({DeviceSizeCategory})",
            densityScale, accessibilityScale, deviceSizeMultiplier, platform, deviceSizeCategory);
    }

    private void SetStandardFontSizes(double accessibilityScale, DevicePlatform platform, double deviceSizeMultiplier)
    {
        // Get platform-specific defaults based on industry standards
        PlatformFontDefaults defaults;
        if (platform == DevicePlatform.iOS)
            defaults = PlatformFontDefaults.iOS;
        else if (platform == DevicePlatform.Android)
            defaults = PlatformFontDefaults.Android;
        else if (platform == DevicePlatform.WinUI)
            defaults = PlatformFontDefaults.Windows;
        else
            // Default to Android for unknown platforms
            defaults = PlatformFontDefaults.Android;

        // Apply device size multiplier to base sizes (tablets/desktop get larger fonts)
        double BaseStandardSize = defaults.StandardSize * deviceSizeMultiplier;
        double BaseHeaderSize = defaults.HeaderSize * deviceSizeMultiplier;
        double BaseSmallSize = defaults.SmallSize * deviceSizeMultiplier;
        double BaseSmallMediumSize = defaults.SmallMediumSize * deviceSizeMultiplier;
        double BaseMediumSize = defaults.MediumSize * deviceSizeMultiplier;
        double BaseLargeSize = defaults.LargeSize * deviceSizeMultiplier;
        double BaseTitleSize = defaults.TitleSize * deviceSizeMultiplier;

        // Icon font sizes (for Font Awesome icons etc.)
        const double BaseIconSmallSize = 14.0;
        const double BaseIconStandardSize = 20.0;
        const double BaseIconLargeSize = 28.0;

        // Apply progressive accessibility scaling: smaller fonts scale more, larger fonts scale less
        // This addresses the issue where users increase OS font size to read small text,
        // but don't want already-large fonts to become unnecessarily huge
        double smallProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        double smallMediumProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        double standardProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        double mediumProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        double largeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        double headerProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        double titleProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Max caps: smaller fonts can scale more, larger fonts have tighter caps
        // Caps are proportional to base sizes to maintain platform-appropriate scaling
        double accessibilityCap = Math.Max(1.0, accessibilityScale);

        // Calculate max caps as multiples of base sizes (allows ~1.5x scaling for accessibility)
        // Note: Density scale is NOT applied to font sizes - density should only affect layout (dp), not text (sp)
        // Font sizes should only scale with accessibility settings, not screen density
        standardFontSize = Math.Min(BaseStandardSize * standardProgressiveScale, BaseStandardSize * 1.5 * accessibilityCap);
        headerFontSize = Math.Min(BaseHeaderSize * headerProgressiveScale, BaseHeaderSize * 1.5);
        smallFontSize = Math.Min(BaseSmallSize * smallProgressiveScale, BaseSmallSize * 1.6 * accessibilityCap);
        smallMediumFontSize = Math.Min(BaseSmallMediumSize * smallMediumProgressiveScale, BaseSmallMediumSize * 1.5 * accessibilityCap);
        mediumFontSize = Math.Min(BaseMediumSize * mediumProgressiveScale, BaseMediumSize * 1.4);
        largeFontSize = Math.Min(BaseLargeSize * largeProgressiveScale, BaseLargeSize * 1.4);
        titleFontSize = Math.Min(BaseTitleSize * titleProgressiveScale, BaseTitleSize * 1.3);

        // Icon fonts: small icons scale more, large icons scale less
        double iconSmallProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        double iconStandardProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        double iconLargeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        iconSmallFontSize = Math.Min(BaseIconSmallSize * iconSmallProgressiveScale, 20.0 * accessibilityCap);
        iconStandardFontSize = Math.Min(BaseIconStandardSize * iconStandardProgressiveScale, 28.0);
        iconLargeFontSize = Math.Min(BaseIconLargeSize * iconLargeProgressiveScale, 40.0);

        // Icon container sizes scale proportionally with their icons
        const double BaseIconSmallContainerSize = 32.0;
        const double BaseIconStandardContainerSize = 48.0;
        const double BaseIconLargeContainerSize = 56.0;

        iconSmallContainerSize = Math.Min(BaseIconSmallContainerSize * iconSmallProgressiveScale, 48.0 * accessibilityCap);
        iconStandardContainerSize = Math.Min(BaseIconStandardContainerSize * iconStandardProgressiveScale, 72.0);
        iconLargeContainerSize = Math.Min(BaseIconLargeContainerSize * iconLargeProgressiveScale, 84.0);

        // Content widths scale conservatively to maintain reasonable UI proportions
        // Use a moderate progressive scale based on a medium base size
        double contentWidthProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(14.0, accessibilityScale);
        const double BaseContentWidthSmall = 200.0;
        const double BaseContentWidthMedium = 240.0;
        const double BaseContentWidthLarge = 280.0;

        contentWidthSmall = Math.Min(BaseContentWidthSmall * contentWidthProgressiveScale, 300.0 * accessibilityCap);
        contentWidthMedium = Math.Min(BaseContentWidthMedium * contentWidthProgressiveScale, 360.0 * accessibilityCap);
        contentWidthLarge = Math.Min(BaseContentWidthLarge * contentWidthProgressiveScale, 420.0 * accessibilityCap);
    }

    private void SetAlarmFontSizes(double accessibilityScale, double widthDp, DevicePlatform platform, double deviceSizeMultiplier)
    {
        // Get platform-specific alarm font defaults per industry standards
        PlatformFontDefaults defaults;
        if (platform == DevicePlatform.iOS)
            defaults = PlatformFontDefaults.iOS;
        else if (platform == DevicePlatform.Android)
            defaults = PlatformFontDefaults.Android;
        else if (platform == DevicePlatform.WinUI)
            defaults = PlatformFontDefaults.Windows;
        else
            defaults = PlatformFontDefaults.Android;

        // Apply device size multiplier - no reduction for alarm time (should be prominent)
        // Alarm time should be large and prominent, so we don't reduce it
        double baseAlarmTimeSize = defaults.AlarmTimeSize * deviceSizeMultiplier;
        double baseAlarmMeridianSize = defaults.AlarmMeridianSize * deviceSizeMultiplier;
        const double BaseAlarmBellIconSize = 80.0;

        bool isPhone = widthDp < 600;
        var maxSizes = FontServiceSizingHelpers.GetAlarmMaxSizes(isPhone, accessibilityScale, platform, deviceSizeMultiplier);

        // Apply progressive accessibility scaling to alarm fonts
        // Maintain progressive scaling hierarchy: larger fonts should scale less than smaller fonts
        // For alarm time, allow moderate scaling (similar to headline, not full like small text)
        double alarmTimeProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseAlarmTimeSize, accessibilityScale);
        // If alarm time is very large (>20pt), it would scale minimally, but we want it to scale
        // at least as much as headline text (20pt) for accessibility, but not more than headline
        if (baseAlarmTimeSize > 20.0 && accessibilityScale > 1.0)
        {
            // Calculate what headline (20pt) would scale to - this is our reference
            double headlineProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(20.0, accessibilityScale);
            // Use the higher of: current minimal scale OR headline scale
            // This ensures alarm time scales at least as much as headline, but maintains hierarchy
            alarmTimeProgressiveScale = Math.Max(alarmTimeProgressiveScale, headlineProgressiveScale);
            // Cap it so it never exceeds headline scaling by more than 10% (maintains hierarchy)
            alarmTimeProgressiveScale = Math.Min(alarmTimeProgressiveScale, headlineProgressiveScale * 1.1);
        }
        
        double alarmMeridianProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(baseAlarmMeridianSize, accessibilityScale);
        double alarmBellIconProgressiveScale = FontServiceSizingHelpers.CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Note: Density scale is NOT applied to font sizes - density should only affect layout (dp), not text (sp)
        // Font sizes should only scale with accessibility settings, not screen density
        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * alarmTimeProgressiveScale, maxSizes.MaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * alarmMeridianProgressiveScale, maxSizes.MaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * alarmBellIconProgressiveScale, maxSizes.MaxBellIcon);
    }

    private void RaiseAllPropertiesChanged() =>
        // Notify that all properties changed - forces all bindings to re-evaluate
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unsubscribe from display info changes
        DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;

        // Unsubscribe from accessibility font scale changes
        accessibilityFontScaleService.FontScaleChanged -= OnFontScaleChanged;
    }

    // Properties (not fields) so bindings work correctly and PropertyChanged can fire
    public double StandardFontSize => standardFontSize;
    public double HeaderFontSize => headerFontSize;
    public double ButtonFontSize => buttonFontSize;
    public double SmallFontSize => smallFontSize;
    public double SmallMediumFontSize => smallMediumFontSize;
    public double MediumFontSize => mediumFontSize;
    public double LargeFontSize => largeFontSize;
    public double TitleFontSize => titleFontSize;
    public double AlarmTimeFontSize => alarmTimeFontSize;
    public double AlarmMeridianFontSize => alarmMeridianFontSize;
    public double AlarmBellIconFontSize => alarmBellIconFontSize;
    public double IconSmallFontSize => iconSmallFontSize;
    public double IconStandardFontSize => iconStandardFontSize;
    public double IconLargeFontSize => iconLargeFontSize;
    public double IconSmallContainerSize => iconSmallContainerSize;
    public double IconStandardContainerSize => iconStandardContainerSize;
    public double IconLargeContainerSize => iconLargeContainerSize;
    public double ContentWidthSmall => contentWidthSmall;
    public double ContentWidthMedium => contentWidthMedium;
    public double ContentWidthLarge => contentWidthLarge;
    public double SpinnerContainerSize => spinnerContainerSize;
    public double SpinnerFontSize => spinnerFontSize;

    public double GetScaledFontSize(double baseSizeInPoints)
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

        // Handle invalid display info
        double density = mainDisplayInfo.Density > 0 ? mainDisplayInfo.Density : 1.0;

        return baseSizeInPoints * Math.Min(density, 2.0);
    }
}

