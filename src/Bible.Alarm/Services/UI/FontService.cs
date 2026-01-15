#nullable enable
using System.ComponentModel;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

    /// <summary>
    /// Platform-specific font size defaults based on industry standards
    /// </summary>
internal record PlatformFontDefaults(
    double StandardSize,    // Body text
    double HeaderSize,       // Headers
    double SmallSize,       // Small text / Caption
    double SmallMediumSize, // Small-medium text
    double MediumSize,      // Medium text
    double LargeSize,       // Large text
    double TitleSize,       // Title / Display text
    double AlarmTimeSize,   // Alarm time in list items (per alarm clock app standards)
    double AlarmMeridianSize // AM/PM in list items
)
{
    /// <summary>
    /// iOS defaults per Apple Human Interface Guidelines
    /// Body: 17pt, Caption: 13pt, Headline: 22pt, Title: 28pt
    /// Alarm time: 36pt for prominent display (per Clock app standards for list items)
    /// </summary>
    public static PlatformFontDefaults iOS { get; } = new(
        StandardSize: 17.0,    // iOS Body (17pt)
        HeaderSize: 22.0,      // iOS Headline (22pt)
        SmallSize: 13.0,       // iOS Caption (13pt)
        SmallMediumSize: 15.0, // iOS Subhead (15pt)
        MediumSize: 17.0,      // iOS Body (17pt)
        LargeSize: 22.0,      // iOS Headline (22pt)
        TitleSize: 28.0,       // iOS Title 1 (28pt)
        AlarmTimeSize: 36.0,   // iOS alarm time (36pt, prominent display size)
        AlarmMeridianSize: 18.0 // iOS AM/PM (18pt, Headline size)
    );

    /// <summary>
    /// Android defaults per Material Design typography scale
    /// Body Large: 16sp, Body Medium: 14sp, Body Small: 12sp, Headline: 20-24sp
    /// Alarm time: 28sp for prominent display (per Material Design and alarm app standards)
    /// </summary>
    public static PlatformFontDefaults Android { get; } = new(
        StandardSize: 16.0,    // Android Body Large (16sp)
        HeaderSize: 20.0,     // Android Headline Medium (20sp)
        SmallSize: 12.0,      // Android Body Small (12sp)
        SmallMediumSize: 14.0, // Android Body Medium (14sp)
        MediumSize: 16.0,     // Android Body Large (16sp)
        LargeSize: 20.0,      // Android Headline Medium (20sp)
        TitleSize: 24.0,       // Android Headline Large (24sp)
        AlarmTimeSize: 28.0,   // Android alarm time (28sp, prominent display size)
        AlarmMeridianSize: 18.0 // Android AM/PM (18sp, Headline Medium size)
    );

    /// <summary>
    /// Windows defaults per Fluent Design System
    /// Body: 14pt (desktop standard), Header: 18pt, Title: 24pt
    /// Alarm time: 32pt for prominent display (larger for desktop readability)
    /// </summary>
    public static PlatformFontDefaults Windows { get; } = new(
        StandardSize: 14.0,    // Windows Body (14pt)
        HeaderSize: 18.0,      // Windows Header (18pt)
        SmallSize: 12.0,       // Windows Caption (12pt)
        SmallMediumSize: 13.0, // Windows Small (13pt)
        MediumSize: 14.0,      // Windows Body (14pt)
        LargeSize: 18.0,       // Windows Header (18pt)
        TitleSize: 24.0,       // Windows Title (24pt)
        AlarmTimeSize: 32.0,   // Windows alarm time (32pt, prominent display size)
        AlarmMeridianSize: 18.0 // Windows AM/PM (18pt, matches Header)
    );
}

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

    private readonly IAccessibilityFontScaleService accessibilityFontScaleService;

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
        var alarmReduction = GetAndroidAlarmReduction(isAndroid);

        if (!hasValidDisplayInfo)
        {
            SetFallbackFontSizes(platform, isAndroid, alarmReduction);
        }
        else
        {
            SetScaledFontSizes(mainDisplayInfo, isAndroid, alarmReduction);
        }

        buttonFontSize = headerFontSize - 1.0;
        RaiseAllPropertiesChanged();
    }

    private static bool HasValidDisplayInfo(DisplayInfo displayInfo)
    {
        return displayInfo.Width > 0 &&
               displayInfo.Height > 0 &&
               displayInfo.Density > 0;
    }

    private static double GetAndroidAlarmReduction(bool isAndroid)
    {
        return isAndroid ? 0.75 : 1.0;
    }

    private void SetFallbackFontSizes(DevicePlatform platform, bool isAndroid, double androidAlarmReduction)
    {
        var deviceIdiom = DeviceInfo.Idiom;
        
        // Determine device size category for fallback (use screen width if available, otherwise use idiom)
        var deviceSizeCategory = deviceIdiom == DeviceIdiom.Desktop 
            ? DeviceSizeCategory.Desktop 
            : DeviceSizeCategory.Phone; // Default to phone if we can't determine
        double deviceSizeMultiplier = GetDeviceSizeMultiplier(deviceSizeCategory, platform);

        if (platform == DevicePlatform.WinUI || deviceIdiom == DeviceIdiom.Desktop)
        {
            SetWindowsDesktopFallbackFontSizes(androidAlarmReduction, deviceSizeMultiplier);
        }
        else
        {
            SetOtherPlatformFallbackFontSizes(isAndroid, androidAlarmReduction, deviceSizeMultiplier);
        }
    }

    private void SetWindowsDesktopFallbackFontSizes(double androidAlarmReduction, double deviceSizeMultiplier)
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

        standardFontSize = BaseStandardSize * CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        headerFontSize = BaseHeaderSize * CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        smallFontSize = BaseSmallSize * CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        smallMediumFontSize = BaseSmallMediumSize * CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        mediumFontSize = BaseMediumSize * CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        largeFontSize = BaseLargeSize * CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        titleFontSize = BaseTitleSize * CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Alarm fonts use platform-specific defaults per industry standards
        var windowsDefaults = PlatformFontDefaults.Windows;
        double BaseAlarmTimeSize = windowsDefaults.AlarmTimeSize * deviceSizeMultiplier;
        double BaseAlarmMeridianSize = windowsDefaults.AlarmMeridianSize * deviceSizeMultiplier;
        const double BaseAlarmBellIconSize = 80.0;

        // Apply progressive accessibility scaling with boost for alarm time
        double alarmTimeProgressiveScale = CalculateProgressiveAccessibilityScale(BaseAlarmTimeSize, accessibilityScale);
        // Boost the progressive scale for alarm time to allow better accessibility scaling
        if (BaseAlarmTimeSize > 20.0 && accessibilityScale > 1.0)
        {
            double baseProgressiveScale = alarmTimeProgressiveScale;
            double extraBoost = (accessibilityScale - 1.0) * 0.3; // Add 30% more scaling
            alarmTimeProgressiveScale = Math.Min(baseProgressiveScale + extraBoost, accessibilityScale);
        }
        
        alarmTimeFontSize = BaseAlarmTimeSize * alarmTimeProgressiveScale;
        alarmMeridianFontSize = BaseAlarmMeridianSize * CalculateProgressiveAccessibilityScale(BaseAlarmMeridianSize, accessibilityScale);
        alarmBellIconFontSize = BaseAlarmBellIconSize * CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Icon font sizes
        const double BaseIconSmallSize = 14.0;
        const double BaseIconStandardSize = 20.0;
        const double BaseIconLargeSize = 28.0;

        iconSmallFontSize = BaseIconSmallSize * CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        iconStandardFontSize = BaseIconStandardSize * CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        iconLargeFontSize = BaseIconLargeSize * CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        // Icon container sizes scale proportionally with their icons
        const double BaseIconSmallContainerSize = 32.0;
        const double BaseIconStandardContainerSize = 48.0;
        const double BaseIconLargeContainerSize = 56.0;

        double iconSmallProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        double iconStandardProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        double iconLargeProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        iconSmallContainerSize = BaseIconSmallContainerSize * iconSmallProgressiveScale;
        iconStandardContainerSize = BaseIconStandardContainerSize * iconStandardProgressiveScale;
        iconLargeContainerSize = BaseIconLargeContainerSize * iconLargeProgressiveScale;

        // Content widths scale conservatively
        double contentWidthProgressiveScale = CalculateProgressiveAccessibilityScale(14.0, accessibilityScale);
        contentWidthSmall = 200.0 * contentWidthProgressiveScale;
        contentWidthMedium = 240.0 * contentWidthProgressiveScale;
        contentWidthLarge = 280.0 * contentWidthProgressiveScale;

        Log.Logger.Debug("Using Windows desktop fallback font sizes (Body: {Body}pt, Header: {Header}pt) with device size multiplier {DeviceSizeMultiplier:F2} and progressive accessibility scale {AccessibilityScale:F2}",
            BaseStandardSize, BaseHeaderSize, deviceSizeMultiplier, accessibilityScale);
    }

    private void SetOtherPlatformFallbackFontSizes(bool isAndroid, double androidAlarmReduction, double deviceSizeMultiplier)
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

        standardFontSize = BaseStandardSize * CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        headerFontSize = BaseHeaderSize * CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        smallFontSize = BaseSmallSize * CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        smallMediumFontSize = BaseSmallMediumSize * CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        mediumFontSize = BaseMediumSize * CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        largeFontSize = BaseLargeSize * CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        titleFontSize = BaseTitleSize * CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Icon font sizes
        const double BaseIconSmallSize = 14.0;
        const double BaseIconStandardSize = 20.0;
        const double BaseIconLargeSize = 28.0;

        iconSmallFontSize = BaseIconSmallSize * CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        iconStandardFontSize = BaseIconStandardSize * CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        iconLargeFontSize = BaseIconLargeSize * CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        // Icon container sizes scale proportionally with their icons
        const double BaseIconSmallContainerSize = 32.0;
        const double BaseIconStandardContainerSize = 48.0;
        const double BaseIconLargeContainerSize = 56.0;

        double iconSmallProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        double iconStandardProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        double iconLargeProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

        iconSmallContainerSize = BaseIconSmallContainerSize * iconSmallProgressiveScale;
        iconStandardContainerSize = BaseIconStandardContainerSize * iconStandardProgressiveScale;
        iconLargeContainerSize = BaseIconLargeContainerSize * iconLargeProgressiveScale;

        // Content widths scale conservatively
        double contentWidthProgressiveScale = CalculateProgressiveAccessibilityScale(14.0, accessibilityScale);
        contentWidthSmall = 200.0 * contentWidthProgressiveScale;
        contentWidthMedium = 240.0 * contentWidthProgressiveScale;
        contentWidthLarge = 280.0 * contentWidthProgressiveScale;

        // Alarm fonts use platform-specific defaults per industry standards
        var platformDefaults = isAndroid ? PlatformFontDefaults.Android : PlatformFontDefaults.iOS;
        // No reduction for alarm time - should be prominent and large
        double baseAlarmTimeSize = platformDefaults.AlarmTimeSize * deviceSizeMultiplier;
        double baseAlarmMeridianSize = platformDefaults.AlarmMeridianSize * deviceSizeMultiplier;
        const double BaseAlarmBellIconSize = 80.0;

        // Apply progressive accessibility scaling with boost for alarm time
        double alarmTimeProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmTimeSize, accessibilityScale);
        // Boost the progressive scale for alarm time to allow better accessibility scaling
        if (baseAlarmTimeSize > 20.0 && accessibilityScale > 1.0)
        {
            double baseProgressiveScale = alarmTimeProgressiveScale;
            double extraBoost = (accessibilityScale - 1.0) * 0.3; // Add 30% more scaling
            alarmTimeProgressiveScale = Math.Min(baseProgressiveScale + extraBoost, accessibilityScale);
        }
        
        double alarmMeridianProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmMeridianSize, accessibilityScale);
        double alarmBellIconProgressiveScale = CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Apply max caps for alarm fonts (allow up to 2.5x for accessibility)
        double maxProgressiveScale = CalculateProgressiveAccessibilityScale(30.0, accessibilityScale);
        double maxScaleFactor = 1.0 + ((maxProgressiveScale - 1.0) * 0.5);
        double fallbackMaxTime = baseAlarmTimeSize * 2.5 * maxScaleFactor;
        double fallbackMaxMeridian = baseAlarmMeridianSize * 2.0 * maxScaleFactor;

        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * alarmTimeProgressiveScale, fallbackMaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * alarmMeridianProgressiveScale, fallbackMaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * alarmBellIconProgressiveScale, 100.0 * maxScaleFactor);

        Log.Logger.Warning("Invalid display info detected on {Platform} platform, using fallback font sizes (Body: {Body}pt, Header: {Header}pt) with device size multiplier {DeviceSizeMultiplier:F2} and progressive accessibility scale {AccessibilityScale:F2}",
            isAndroid ? "Android" : "iOS", BaseStandardSize, BaseHeaderSize, deviceSizeMultiplier, accessibilityScale);
    }

    private void SetScaledFontSizes(DisplayInfo mainDisplayInfo, bool isAndroid, double androidAlarmReduction)
    {
        double density = mainDisplayInfo.Density;
        double widthDp = mainDisplayInfo.Width / density;

        // Get the OS accessibility font scale (1.0 = normal, >1.0 = larger for accessibility)
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        var platform = DeviceInfo.Platform;
        var deviceIdiom = DeviceInfo.Idiom;
        
        // Determine device size category and multiplier
        var deviceSizeCategory = GetDeviceSizeCategory(widthDp, deviceIdiom);
        double deviceSizeMultiplier = GetDeviceSizeMultiplier(deviceSizeCategory, platform);
        
        double densityScale = CalculateDensityScaleFactor(widthDp, density);
        SetStandardFontSizes(densityScale, accessibilityScale, platform, deviceSizeMultiplier);
        SetAlarmFontSizes(densityScale, accessibilityScale, widthDp, isAndroid, androidAlarmReduction, platform, deviceSizeMultiplier);

        Log.Logger.Debug("Font sizes updated with density scale {DensityScale:F2}, accessibility scale {AccessibilityScale:F2}, device size multiplier {DeviceSizeMultiplier:F2} for platform {Platform} ({DeviceSizeCategory})",
            densityScale, accessibilityScale, deviceSizeMultiplier, platform, deviceSizeCategory);
    }

    /// <summary>
    /// Device size categories based on screen width in density-independent pixels
    /// Following industry standards: phones < 600dp, tablets 600-960dp, desktop > 960dp
    /// </summary>
    private enum DeviceSizeCategory
    {
        Phone,      // < 600dp (small to large phones)
        Tablet,     // 600-960dp (tablets, foldables)
        Desktop     // > 960dp (desktop, large tablets)
    }

    /// <summary>
    /// Determines device size category based on screen width
    /// </summary>
    private static DeviceSizeCategory GetDeviceSizeCategory(double widthDp, DeviceIdiom idiom)
    {
        // Desktop idiom always uses desktop category
        if (idiom == DeviceIdiom.Desktop)
        {
            return DeviceSizeCategory.Desktop;
        }

        // Use width-based categorization per Material Design and iOS guidelines
        return widthDp < 600 ? DeviceSizeCategory.Phone :
               widthDp < 960 ? DeviceSizeCategory.Tablet :
               DeviceSizeCategory.Desktop;
    }

    /// <summary>
    /// Gets device size multiplier for font scaling per industry standards
    /// - Phones: 1.0x (base size)
    /// - Tablets: 1.15-1.20x (slightly larger for better readability at viewing distance)
    /// - Desktop: 1.0-1.1x (desktop apps typically use consistent sizes)
    /// </summary>
    private static double GetDeviceSizeMultiplier(DeviceSizeCategory category, DevicePlatform platform)
    {
        if (category == DeviceSizeCategory.Phone)
        {
            return 1.0; // Base size for phones
        }
        
        if (category == DeviceSizeCategory.Tablet)
        {
            if (platform == DevicePlatform.iOS)
                return 1.15;      // iOS tablets: 15% larger (per HIG recommendations)
            if (platform == DevicePlatform.Android)
                return 1.20;      // Android tablets: 20% larger (per Material Design)
            return 1.15;          // Default for other platforms
        }
        
        if (category == DeviceSizeCategory.Desktop)
        {
            if (platform == DevicePlatform.WinUI)
                return 1.0;       // Windows desktop: standard size
            return 1.1;           // Other desktop: slight increase
        }
        
        return 1.0;
    }

    private static double CalculateDensityScaleFactor(double widthDp, double density)
    {
        bool isPhone = widthDp < 600;
        bool isTablet = widthDp is >= 600 and < 960;

        return isPhone ? Math.Min(density, 1.5) :
               isTablet ? Math.Min(density, 1.8) :
               Math.Min(density, 2.2);
    }

    /// <summary>
    /// Calculates a progressive accessibility scale factor based on base font size.
    /// Smaller fonts scale more with OS accessibility settings, while larger fonts scale less.
    /// This addresses the issue where users increase OS font size to read small text,
    /// but don't want already-large fonts to become unnecessarily huge.
    /// </summary>
    /// <param name="baseFontSize">The base font size in points</param>
    /// <param name="accessibilityScale">The OS accessibility font scale (1.0 = normal, >1.0 = larger)</param>
    /// <returns>A progressive scale factor that decreases as base font size increases</returns>
    private static double CalculateProgressiveAccessibilityScale(double baseFontSize, double accessibilityScale)
    {
        // If accessibility scale is 1.0 (normal), no scaling needed
        if (accessibilityScale <= 1.0)
        {
            return 1.0;
        }

        // Calculate how much extra scaling is needed beyond normal
        double extraScale = accessibilityScale - 1.0;

        // Progressive scaling based on font size:
        // - Very small fonts (<=10pt): scale fully (100% of extra scale)
        // - Small fonts (10-12pt): scale fully (100% of extra scale)
        // - Medium fonts (12-16pt): scale moderately (50% of extra scale)
        // - Large fonts (16-20pt): scale minimally (25% of extra scale)
        // - Very large fonts (>20pt): scale very minimally (10% of extra scale)

        double progressiveFactor;
        if (baseFontSize <= 10.0)
        {
            // Very small fonts: full scaling
            progressiveFactor = 1.0;
        }
        else if (baseFontSize <= 12.0)
        {
            // Small fonts: full scaling
            progressiveFactor = 1.0;
        }
        else if (baseFontSize <= 16.0)
        {
            // Medium fonts: moderate scaling (interpolate from 1.0 at 12pt to 0.5 at 16pt)
            progressiveFactor = 1.0 - ((baseFontSize - 12.0) / 4.0) * 0.5;
        }
        else if (baseFontSize <= 20.0)
        {
            // Large fonts: minimal scaling (interpolate from 0.5 at 16pt to 0.25 at 20pt)
            progressiveFactor = 0.5 - ((baseFontSize - 16.0) / 4.0) * 0.25;
        }
        else
        {
            // Very large fonts: very minimal scaling (interpolate from 0.25 at 20pt to 0.1 at 30pt+)
            progressiveFactor = Math.Max(0.1, 0.25 - ((baseFontSize - 20.0) / 10.0) * 0.15);
        }

        // Apply progressive factor to extra scale, then add back the base 1.0
        return 1.0 + (extraScale * progressiveFactor);
    }

    private void SetStandardFontSizes(double densityScale, double accessibilityScale, DevicePlatform platform, double deviceSizeMultiplier)
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
            defaults = PlatformFontDefaults.Android; // Default to Android for unknown platforms

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
        double smallProgressiveScale = CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        double smallMediumProgressiveScale = CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        double standardProgressiveScale = CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        double mediumProgressiveScale = CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        double largeProgressiveScale = CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        double headerProgressiveScale = CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        double titleProgressiveScale = CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Max caps: smaller fonts can scale more, larger fonts have tighter caps
        // Caps are proportional to base sizes to maintain platform-appropriate scaling
        double accessibilityCap = Math.Max(1.0, accessibilityScale);

        // Calculate max caps as multiples of base sizes (allows ~1.5x scaling for accessibility)
        standardFontSize = Math.Min(BaseStandardSize * densityScale * standardProgressiveScale, BaseStandardSize * 1.5 * accessibilityCap);
        headerFontSize = Math.Min(BaseHeaderSize * densityScale * headerProgressiveScale, BaseHeaderSize * 1.5);
        smallFontSize = Math.Min(BaseSmallSize * densityScale * smallProgressiveScale, BaseSmallSize * 1.6 * accessibilityCap);
        smallMediumFontSize = Math.Min(BaseSmallMediumSize * densityScale * smallMediumProgressiveScale, BaseSmallMediumSize * 1.5 * accessibilityCap);
        mediumFontSize = Math.Min(BaseMediumSize * densityScale * mediumProgressiveScale, BaseMediumSize * 1.4);
        largeFontSize = Math.Min(BaseLargeSize * densityScale * largeProgressiveScale, BaseLargeSize * 1.4);
        titleFontSize = Math.Min(BaseTitleSize * densityScale * titleProgressiveScale, BaseTitleSize * 1.3);

        // Icon fonts: small icons scale more, large icons scale less
        double iconSmallProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconSmallSize, accessibilityScale);
        double iconStandardProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconStandardSize, accessibilityScale);
        double iconLargeProgressiveScale = CalculateProgressiveAccessibilityScale(BaseIconLargeSize, accessibilityScale);

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
        double contentWidthProgressiveScale = CalculateProgressiveAccessibilityScale(14.0, accessibilityScale);
        const double BaseContentWidthSmall = 200.0;
        const double BaseContentWidthMedium = 240.0;
        const double BaseContentWidthLarge = 280.0;

        contentWidthSmall = Math.Min(BaseContentWidthSmall * contentWidthProgressiveScale, 300.0 * accessibilityCap);
        contentWidthMedium = Math.Min(BaseContentWidthMedium * contentWidthProgressiveScale, 360.0 * accessibilityCap);
        contentWidthLarge = Math.Min(BaseContentWidthLarge * contentWidthProgressiveScale, 420.0 * accessibilityCap);
    }

    private void SetAlarmFontSizes(double densityScale, double accessibilityScale, double widthDp, bool isAndroid, double androidAlarmReduction, DevicePlatform platform, double deviceSizeMultiplier)
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
        var maxSizes = GetAlarmMaxSizes(isPhone, isAndroid, accessibilityScale, platform, deviceSizeMultiplier);

        // Apply progressive accessibility scaling to alarm fonts
        // Alarm fonts should scale more than very large fonts since they're display text
        // Use a more generous scaling factor for alarm time (treat as display/headline text)
        double alarmTimeProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmTimeSize, accessibilityScale);
        // For alarm time, allow more scaling - treat it like headline text (not very large display)
        // If base is > 20pt, the progressive scale is minimal, so boost it for alarm time
        if (baseAlarmTimeSize > 20.0 && accessibilityScale > 1.0)
        {
            // Boost the progressive scale for alarm time to allow better accessibility scaling
            double baseProgressiveScale = alarmTimeProgressiveScale;
            double extraBoost = (accessibilityScale - 1.0) * 0.3; // Add 30% more scaling
            alarmTimeProgressiveScale = Math.Min(baseProgressiveScale + extraBoost, accessibilityScale);
        }
        
        double alarmMeridianProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmMeridianSize, accessibilityScale);
        double alarmBellIconProgressiveScale = CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Allow alarm time to scale more aggressively with density (up to 2.5x for better visibility)
        double alarmTimeDensityScale = Math.Min(densityScale, 2.5);
        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * alarmTimeDensityScale * alarmTimeProgressiveScale, maxSizes.MaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * densityScale * alarmMeridianProgressiveScale, maxSizes.MaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * densityScale * alarmBellIconProgressiveScale, maxSizes.MaxBellIcon);
    }

    private static (double MaxTime, double MaxMeridian, double MaxBellIcon) GetAlarmMaxSizes(bool isPhone, bool isAndroid, double accessibilityScale, DevicePlatform platform, double deviceSizeMultiplier)
    {
        // Alarm fonts are already large, so max sizes should scale minimally with accessibility
        // Use a very minimal progressive scale for the max caps (similar to very large fonts)
        double maxProgressiveScale = CalculateProgressiveAccessibilityScale(30.0, accessibilityScale);
        double maxScaleFactor = 1.0 + ((maxProgressiveScale - 1.0) * 0.5); // Further reduce max scaling

        // Get platform-specific base sizes and calculate max as multiples (allows ~2x scaling for accessibility)
        PlatformFontDefaults defaults;
        if (platform == DevicePlatform.iOS)
            defaults = PlatformFontDefaults.iOS;
        else if (platform == DevicePlatform.Android)
            defaults = PlatformFontDefaults.Android;
        else if (platform == DevicePlatform.WinUI)
            defaults = PlatformFontDefaults.Windows;
        else
            defaults = PlatformFontDefaults.Android;

        double baseTimeSize = defaults.AlarmTimeSize * deviceSizeMultiplier;
        double baseMeridianSize = defaults.AlarmMeridianSize * deviceSizeMultiplier;

        // Max sizes: allow up to 2.5x for accessibility, with higher limits for tablets
        // Increased multipliers to allow alarm time to grow larger
        double maxTimeMultiplier = isPhone ? 2.5 : 3.0;
        double maxMeridianMultiplier = isPhone ? 2.0 : 2.5;

        double maxAlarmTimeSize = baseTimeSize * maxTimeMultiplier * maxScaleFactor;
        double maxAlarmMeridianSize = baseMeridianSize * maxMeridianMultiplier * maxScaleFactor;
        double maxAlarmBellIconSize = (isPhone ? 100.0 : 130.0) * maxScaleFactor;

        return (maxAlarmTimeSize, maxAlarmMeridianSize, maxAlarmBellIconSize);
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

    public double GetScaledFontSize(double baseSizeInPoints)
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

        // Handle invalid display info
        double density = mainDisplayInfo.Density > 0 ? mainDisplayInfo.Density : 1.0;

        return baseSizeInPoints * Math.Min(density, 2.0);
    }
}

