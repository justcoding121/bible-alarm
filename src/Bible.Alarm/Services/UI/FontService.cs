#nullable enable
using System.ComponentModel;
using Bible.Alarm.Common.Interfaces.Platform;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Service for managing scalable font sizes based on device metrics and OS accessibility settings.
/// Uses screen width in density-independent pixels (dp) to determine device category,
/// then applies both density scaling and OS accessibility font scale for optimal readability.
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

        if (platform == DevicePlatform.WinUI || deviceIdiom == DeviceIdiom.Desktop)
        {
            SetWindowsDesktopFallbackFontSizes(androidAlarmReduction);
        }
        else
        {
            SetOtherPlatformFallbackFontSizes(isAndroid, androidAlarmReduction);
        }
    }

    private void SetWindowsDesktopFallbackFontSizes(double androidAlarmReduction)
    {
        // Apply progressive accessibility scaling to fallback sizes
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        const double BaseStandardSize = 14.0;
        const double BaseHeaderSize = 20.0;
        const double BaseSmallSize = 12.0;
        const double BaseSmallMediumSize = 12.0;
        const double BaseMediumSize = 16.0;
        const double BaseLargeSize = 18.0;
        const double BaseTitleSize = 22.0;

        standardFontSize = BaseStandardSize * CalculateProgressiveAccessibilityScale(BaseStandardSize, accessibilityScale);
        headerFontSize = BaseHeaderSize * CalculateProgressiveAccessibilityScale(BaseHeaderSize, accessibilityScale);
        smallFontSize = BaseSmallSize * CalculateProgressiveAccessibilityScale(BaseSmallSize, accessibilityScale);
        smallMediumFontSize = BaseSmallMediumSize * CalculateProgressiveAccessibilityScale(BaseSmallMediumSize, accessibilityScale);
        mediumFontSize = BaseMediumSize * CalculateProgressiveAccessibilityScale(BaseMediumSize, accessibilityScale);
        largeFontSize = BaseLargeSize * CalculateProgressiveAccessibilityScale(BaseLargeSize, accessibilityScale);
        titleFontSize = BaseTitleSize * CalculateProgressiveAccessibilityScale(BaseTitleSize, accessibilityScale);

        // Alarm fonts use minimal scaling
        const double BaseAlarmTimeSize = 35.0;
        const double BaseAlarmMeridianSize = 19.0;
        const double BaseAlarmBellIconSize = 80.0;

        alarmTimeFontSize = BaseAlarmTimeSize * CalculateProgressiveAccessibilityScale(BaseAlarmTimeSize, accessibilityScale);
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

        Log.Logger.Debug("Using Windows desktop fallback font sizes with progressive accessibility scale {AccessibilityScale:F2}",
            accessibilityScale);
    }

    private void SetOtherPlatformFallbackFontSizes(bool isAndroid, double androidAlarmReduction)
    {
        // Apply progressive accessibility scaling to fallback sizes
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        const double BaseStandardSize = 12.0;
        const double BaseHeaderSize = 18.0;
        const double BaseSmallSize = 10.0;
        const double BaseSmallMediumSize = 12.0;
        const double BaseMediumSize = 14.0;
        const double BaseLargeSize = 16.0;
        const double BaseTitleSize = 20.0;

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

        // Alarm fonts use minimal scaling with caps
        double baseAlarmTimeSize = 37.0 * androidAlarmReduction;
        double baseAlarmMeridianSize = 21.0 * androidAlarmReduction;
        const double BaseAlarmBellIconSize = 80.0;

        double alarmTimeProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmTimeSize, accessibilityScale);
        double alarmMeridianProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmMeridianSize, accessibilityScale);
        double alarmBellIconProgressiveScale = CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        // Apply minimal max caps for alarm fonts
        double maxProgressiveScale = CalculateProgressiveAccessibilityScale(30.0, accessibilityScale);
        double maxScaleFactor = 1.0 + ((maxProgressiveScale - 1.0) * 0.5);
        double fallbackMaxTime = (isAndroid ? 36.0 : 48.0) * maxScaleFactor;
        double fallbackMaxMeridian = (isAndroid ? 20.0 : 26.0) * maxScaleFactor;

        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * alarmTimeProgressiveScale, fallbackMaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * alarmMeridianProgressiveScale, fallbackMaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * alarmBellIconProgressiveScale, 100.0 * maxScaleFactor);

        Log.Logger.Warning("Invalid display info detected on non-Windows platform, using fallback font sizes with progressive accessibility scale {AccessibilityScale:F2}",
            accessibilityScale);
    }

    private void SetScaledFontSizes(DisplayInfo mainDisplayInfo, bool isAndroid, double androidAlarmReduction)
    {
        double density = mainDisplayInfo.Density;
        double widthDp = mainDisplayInfo.Width / density;

        // Get the OS accessibility font scale (1.0 = normal, >1.0 = larger for accessibility)
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        double densityScale = CalculateDensityScaleFactor(widthDp, density);
        SetStandardFontSizes(densityScale, accessibilityScale);
        SetAlarmFontSizes(densityScale, accessibilityScale, widthDp, isAndroid, androidAlarmReduction);

        Log.Logger.Debug("Font sizes updated with density scale {DensityScale:F2} and accessibility scale {AccessibilityScale:F2}",
            densityScale, accessibilityScale);
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

    private void SetStandardFontSizes(double densityScale, double accessibilityScale)
    {
        const double BaseStandardSize = 12.0;
        const double BaseHeaderSize = 18.0;
        const double BaseSmallSize = 10.0;
        const double BaseSmallMediumSize = 12.0;
        const double BaseMediumSize = 14.0;
        const double BaseLargeSize = 16.0;
        const double BaseTitleSize = 20.0;

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
        double accessibilityCap = Math.Max(1.0, accessibilityScale);

        standardFontSize = Math.Min(BaseStandardSize * densityScale * standardProgressiveScale, 17.0 * accessibilityCap);
        headerFontSize = Math.Min(BaseHeaderSize * densityScale * headerProgressiveScale, 26.0);
        smallFontSize = Math.Min(BaseSmallSize * densityScale * smallProgressiveScale, 14.0 * accessibilityCap);
        smallMediumFontSize = Math.Min(BaseSmallMediumSize * densityScale * smallMediumProgressiveScale, 16.0 * accessibilityCap);
        mediumFontSize = Math.Min(BaseMediumSize * densityScale * mediumProgressiveScale, 19.0);
        largeFontSize = Math.Min(BaseLargeSize * densityScale * largeProgressiveScale, 22.0);
        titleFontSize = Math.Min(BaseTitleSize * densityScale * titleProgressiveScale, 30.0);

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

    private void SetAlarmFontSizes(double densityScale, double accessibilityScale, double widthDp, bool isAndroid, double androidAlarmReduction)
    {
        double baseAlarmTimeSize = 37.0 * androidAlarmReduction;
        double baseAlarmMeridianSize = 21.0 * androidAlarmReduction;
        const double BaseAlarmBellIconSize = 80.0;

        bool isPhone = widthDp < 600;
        var maxSizes = GetAlarmMaxSizes(isPhone, isAndroid, accessibilityScale);

        // Apply progressive accessibility scaling to alarm fonts
        // Alarm fonts are already large, so they should scale minimally
        double alarmTimeProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmTimeSize, accessibilityScale);
        double alarmMeridianProgressiveScale = CalculateProgressiveAccessibilityScale(baseAlarmMeridianSize, accessibilityScale);
        double alarmBellIconProgressiveScale = CalculateProgressiveAccessibilityScale(BaseAlarmBellIconSize, accessibilityScale);

        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * densityScale * alarmTimeProgressiveScale, maxSizes.MaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * densityScale * alarmMeridianProgressiveScale, maxSizes.MaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * densityScale * alarmBellIconProgressiveScale, maxSizes.MaxBellIcon);
    }

    private static (double MaxTime, double MaxMeridian, double MaxBellIcon) GetAlarmMaxSizes(bool isPhone, bool isAndroid, double accessibilityScale)
    {
        // Alarm fonts are already large, so max sizes should scale minimally with accessibility
        // Use a very minimal progressive scale for the max caps (similar to very large fonts)
        double maxProgressiveScale = CalculateProgressiveAccessibilityScale(30.0, accessibilityScale);
        double maxScaleFactor = 1.0 + ((maxProgressiveScale - 1.0) * 0.5); // Further reduce max scaling

        double maxAlarmTimeSize = (isPhone
            ? (isAndroid ? 36.0 : 34.0)
            : (isAndroid ? 45.0 : 60.0)) * maxScaleFactor;
        double maxAlarmMeridianSize = (isPhone
            ? (isAndroid ? 20.0 : 22.0)
            : (isAndroid ? 25.0 : 34.0)) * maxScaleFactor;
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

