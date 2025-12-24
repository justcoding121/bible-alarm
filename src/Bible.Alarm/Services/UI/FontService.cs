#nullable enable
using System.ComponentModel;
using Bible.Alarm.Services.UI.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Service for managing scalable font sizes based on device metrics.
/// Uses screen width in density-independent pixels (dp) to determine device category,
/// then applies density scaling with appropriate caps for optimal readability.
/// Automatically recalculates when screen size/orientation changes.
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

    public FontService()
    {
        // Listen for screen size/orientation changes
        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;

        // Initial calculation
        Recalculate();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => Recalculate();

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
        standardFontSize = 14.0;
        headerFontSize = 20.0;
        smallFontSize = 12.0;
        mediumFontSize = 16.0;
        largeFontSize = 18.0;
        titleFontSize = 22.0;

        alarmTimeFontSize = 30.0;
        alarmMeridianFontSize = 16.0;
        alarmBellIconFontSize = 80.0;

        Log.Logger.Debug("Using Windows desktop fallback fixed font sizes (display info not available)");
    }

    private void SetOtherPlatformFallbackFontSizes(bool isAndroid, double androidAlarmReduction)
    {
        const double BaseStandardSize = 12.0;
        const double BaseHeaderSize = 18.0;
        const double BaseSmallSize = 10.0;
        const double BaseMediumSize = 14.0;
        const double BaseLargeSize = 16.0;
        const double BaseTitleSize = 20.0;

        standardFontSize = BaseStandardSize;
        headerFontSize = BaseHeaderSize;
        smallFontSize = BaseSmallSize;
        mediumFontSize = BaseMediumSize;
        largeFontSize = BaseLargeSize;
        titleFontSize = BaseTitleSize;

        double baseAlarmTimeSize = 32.0 * androidAlarmReduction;
        double baseAlarmMeridianSize = 18.0 * androidAlarmReduction;
        const double BaseAlarmBellIconSize = 80.0;

        double fallbackMaxTime = isAndroid ? 30.0 : 40.0;
        double fallbackMaxMeridian = isAndroid ? 16.0 : 22.0;
        alarmTimeFontSize = Math.Min(baseAlarmTimeSize, fallbackMaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize, fallbackMaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize, 100.0);

        Log.Logger.Warning("Invalid display info detected on non-Windows platform, using base font sizes as fallback");
    }

    private void SetScaledFontSizes(DisplayInfo mainDisplayInfo, bool isAndroid, double androidAlarmReduction)
    {
        double density = mainDisplayInfo.Density;
        double widthDp = mainDisplayInfo.Width / density;

        double scale = CalculateScaleFactor(widthDp, density);
        SetStandardFontSizes(scale);
        SetAlarmFontSizes(scale, widthDp, isAndroid, androidAlarmReduction);
    }

    private static double CalculateScaleFactor(double widthDp, double density)
    {
        bool isPhone = widthDp < 600;
        bool isTablet = widthDp is >= 600 and < 960;

        return isPhone ? Math.Min(density, 1.5) :
               isTablet ? Math.Min(density, 1.8) :
               Math.Min(density, 2.2);
    }

    private void SetStandardFontSizes(double scale)
    {
        const double BaseStandardSize = 12.0;
        const double BaseHeaderSize = 18.0;
        const double BaseSmallSize = 10.0;
        const double BaseMediumSize = 14.0;
        const double BaseLargeSize = 16.0;
        const double BaseTitleSize = 20.0;

        standardFontSize = Math.Min(BaseStandardSize * scale, 17.0);
        headerFontSize = Math.Min(BaseHeaderSize * scale, 26.0);
        smallFontSize = Math.Min(BaseSmallSize * scale, 14.0);
        mediumFontSize = Math.Min(BaseMediumSize * scale, 19.0);
        largeFontSize = Math.Min(BaseLargeSize * scale, 22.0);
        titleFontSize = Math.Min(BaseTitleSize * scale, 30.0);
    }

    private void SetAlarmFontSizes(double scale, double widthDp, bool isAndroid, double androidAlarmReduction)
    {
        double baseAlarmTimeSize = 32.0 * androidAlarmReduction;
        double baseAlarmMeridianSize = 18.0 * androidAlarmReduction;
        const double BaseAlarmBellIconSize = 80.0;

        bool isPhone = widthDp < 600;
        var maxSizes = GetAlarmMaxSizes(isPhone, isAndroid);

        alarmTimeFontSize = Math.Min(baseAlarmTimeSize * scale, maxSizes.MaxTime);
        alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * scale, maxSizes.MaxMeridian);
        alarmBellIconFontSize = Math.Min(BaseAlarmBellIconSize * scale, maxSizes.MaxBellIcon);
    }

    private static (double MaxTime, double MaxMeridian, double MaxBellIcon) GetAlarmMaxSizes(bool isPhone, bool isAndroid)
    {
        double maxAlarmTimeSize = isPhone
            ? (isAndroid ? 30.0 : 40.0)
            : (isAndroid ? 38.0 : 50.0);
        double maxAlarmMeridianSize = isPhone
            ? (isAndroid ? 16.0 : 22.0)
            : (isAndroid ? 21.0 : 28.0);
        double maxAlarmBellIconSize = isPhone ? 100.0 : 130.0;

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

        // No injected services to dispose (this service has no dependencies)
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

    public double GetScaledFontSize(double baseSizeInPoints)
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

        // Handle invalid display info
        double density = mainDisplayInfo.Density > 0 ? mainDisplayInfo.Density : 1.0;

        return baseSizeInPoints * Math.Min(density, 2.0);
    }
}

