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
        // Apply accessibility scale factor even to fallback sizes
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        standardFontSize = 14.0 * accessibilityScale;
        headerFontSize = 20.0 * accessibilityScale;
        smallFontSize = 12.0 * accessibilityScale;
        mediumFontSize = 16.0 * accessibilityScale;
        largeFontSize = 18.0 * accessibilityScale;
        titleFontSize = 22.0 * accessibilityScale;

        alarmTimeFontSize = 30.0 * accessibilityScale;
        alarmMeridianFontSize = 16.0 * accessibilityScale;
        alarmBellIconFontSize = 80.0 * accessibilityScale;

        // Icon font sizes
        iconSmallFontSize = 14.0 * accessibilityScale;
        iconStandardFontSize = 20.0 * accessibilityScale;
        iconLargeFontSize = 28.0 * accessibilityScale;

        // Icon container sizes
        iconSmallContainerSize = 32.0 * accessibilityScale;
        iconStandardContainerSize = 48.0 * accessibilityScale;
        iconLargeContainerSize = 56.0 * accessibilityScale;

        Log.Logger.Debug("Using Windows desktop fallback font sizes with accessibility scale {AccessibilityScale:F2}",
            accessibilityScale);
    }

    private void SetOtherPlatformFallbackFontSizes(bool isAndroid, double androidAlarmReduction)
    {
        // Apply accessibility scale factor even to fallback sizes
        double accessibilityScale = accessibilityFontScaleService.FontScale;

        const double BaseStandardSize = 12.0;
        const double BaseHeaderSize = 18.0;
        const double BaseSmallSize = 10.0;
        const double BaseMediumSize = 14.0;
        const double BaseLargeSize = 16.0;
        const double BaseTitleSize = 20.0;

        standardFontSize = BaseStandardSize * accessibilityScale;
        headerFontSize = BaseHeaderSize * accessibilityScale;
        smallFontSize = BaseSmallSize * accessibilityScale;
        mediumFontSize = BaseMediumSize * accessibilityScale;
        largeFontSize = BaseLargeSize * accessibilityScale;
        titleFontSize = BaseTitleSize * accessibilityScale;

        // Icon font sizes
        iconSmallFontSize = 14.0 * accessibilityScale;
        iconStandardFontSize = 20.0 * accessibilityScale;
        iconLargeFontSize = 28.0 * accessibilityScale;

        // Icon container sizes
        iconSmallContainerSize = 32.0 * accessibilityScale;
        iconStandardContainerSize = 48.0 * accessibilityScale;
        iconLargeContainerSize = 56.0 * accessibilityScale;

        double baseAlarmTimeSize = 32.0 * androidAlarmReduction;
        double baseAlarmMeridianSize = 18.0 * androidAlarmReduction;
        const double BaseAlarmBellIconSize = 80.0;

        double accessibilityCap = Math.Max(1.0, accessibilityScale);
        double fallbackMaxTime = (isAndroid ? 30.0 : 40.0) * accessibilityCap;
        double fallbackMaxMeridian = (isAndroid ? 16.0 : 22.0) * accessibilityCap;
        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * accessibilityScale, fallbackMaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * accessibilityScale, fallbackMaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * accessibilityScale, 100.0 * accessibilityCap);

        Log.Logger.Warning("Invalid display info detected on non-Windows platform, using fallback font sizes with accessibility scale {AccessibilityScale:F2}",
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

    private void SetStandardFontSizes(double densityScale, double accessibilityScale)
    {
        const double BaseStandardSize = 12.0;
        const double BaseHeaderSize = 18.0;
        const double BaseSmallSize = 10.0;
        const double BaseMediumSize = 14.0;
        const double BaseLargeSize = 16.0;
        const double BaseTitleSize = 20.0;

        // Icon font sizes (for Font Awesome icons etc.)
        const double BaseIconSmallSize = 14.0;
        const double BaseIconStandardSize = 20.0;
        const double BaseIconLargeSize = 28.0;

        // Apply both density scaling and accessibility scaling
        // Max caps are also scaled by accessibility factor to allow larger fonts for accessibility
        double accessibilityCap = Math.Max(1.0, accessibilityScale);

        standardFontSize = Math.Min(BaseStandardSize * densityScale * accessibilityScale, 17.0 * accessibilityCap);
        headerFontSize = Math.Min(BaseHeaderSize * densityScale * accessibilityScale, 26.0 * accessibilityCap);
        smallFontSize = Math.Min(BaseSmallSize * densityScale * accessibilityScale, 14.0 * accessibilityCap);
        mediumFontSize = Math.Min(BaseMediumSize * densityScale * accessibilityScale, 19.0 * accessibilityCap);
        largeFontSize = Math.Min(BaseLargeSize * densityScale * accessibilityScale, 22.0 * accessibilityCap);
        titleFontSize = Math.Min(BaseTitleSize * densityScale * accessibilityScale, 30.0 * accessibilityCap);

        // Icon fonts also scale with accessibility - icons should be legible at larger sizes
        iconSmallFontSize = Math.Min(BaseIconSmallSize * accessibilityScale, 20.0 * accessibilityCap);
        iconStandardFontSize = Math.Min(BaseIconStandardSize * accessibilityScale, 28.0 * accessibilityCap);
        iconLargeFontSize = Math.Min(BaseIconLargeSize * accessibilityScale, 40.0 * accessibilityCap);

        // Icon container sizes scale with accessibility so containers grow with icons
        // Container should be approximately 2x the icon size for proper padding
        const double BaseIconSmallContainerSize = 32.0;
        const double BaseIconStandardContainerSize = 48.0;
        const double BaseIconLargeContainerSize = 56.0;

        iconSmallContainerSize = Math.Min(BaseIconSmallContainerSize * accessibilityScale, 48.0 * accessibilityCap);
        iconStandardContainerSize = Math.Min(BaseIconStandardContainerSize * accessibilityScale, 72.0 * accessibilityCap);
        iconLargeContainerSize = Math.Min(BaseIconLargeContainerSize * accessibilityScale, 84.0 * accessibilityCap);
    }

    private void SetAlarmFontSizes(double densityScale, double accessibilityScale, double widthDp, bool isAndroid, double androidAlarmReduction)
    {
        double baseAlarmTimeSize = 32.0 * androidAlarmReduction;
        double baseAlarmMeridianSize = 18.0 * androidAlarmReduction;
        const double BaseAlarmBellIconSize = 80.0;

        bool isPhone = widthDp < 600;
        var maxSizes = GetAlarmMaxSizes(isPhone, isAndroid, accessibilityScale);

        // Apply both density scaling and accessibility scaling to alarm fonts
        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * densityScale * accessibilityScale, maxSizes.MaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * densityScale * accessibilityScale, maxSizes.MaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * densityScale * accessibilityScale, maxSizes.MaxBellIcon);
    }

    private static (double MaxTime, double MaxMeridian, double MaxBellIcon) GetAlarmMaxSizes(bool isPhone, bool isAndroid, double accessibilityScale)
    {
        // Scale the max sizes by accessibility factor to allow larger fonts when user needs them
        double accessibilityCap = Math.Max(1.0, accessibilityScale);

        double maxAlarmTimeSize = (isPhone
            ? (isAndroid ? 30.0 : 28.0)
            : (isAndroid ? 38.0 : 50.0)) * accessibilityCap;
        double maxAlarmMeridianSize = (isPhone
            ? (isAndroid ? 16.0 : 18.0)
            : (isAndroid ? 21.0 : 28.0)) * accessibilityCap;
        double maxAlarmBellIconSize = (isPhone ? 100.0 : 130.0) * accessibilityCap;

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

    public double GetScaledFontSize(double baseSizeInPoints)
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

        // Handle invalid display info
        double density = mainDisplayInfo.Density > 0 ? mainDisplayInfo.Density : 1.0;

        return baseSizeInPoints * Math.Min(density, 2.0);
    }
}

