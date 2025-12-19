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
public class FontService : IFontService, INotifyPropertyChanged, IDisposable
{
    private bool _isDisposed;
    private double _standardFontSize;
    private double _headerFontSize;
    private double _buttonFontSize;
    private double _smallFontSize;
    private double _mediumFontSize;
    private double _largeFontSize;
    private double _titleFontSize;
    private double _alarmTimeFontSize;
    private double _alarmMeridianFontSize;
    private double _alarmBellIconFontSize;

    public FontService()
    {
        // Listen for screen size/orientation changes
        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;

        // Initial calculation
        Recalculate();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

        // Handle case where display info is invalid (common on Windows during startup)
        // Check if we have valid display information
        bool hasValidDisplayInfo = mainDisplayInfo.Width > 0 &&
                                   mainDisplayInfo.Height > 0 &&
                                   mainDisplayInfo.Density > 0;

        double density;
        double widthDp;

        // Base sizes in points (standard practice: 12pt base)
        const double baseStandardSize = 12.0;
        const double baseHeaderSize = 18.0;
        const double baseSmallSize = 10.0;
        const double baseMediumSize = 14.0;
        const double baseLargeSize = 16.0;
        const double baseTitleSize = 20.0;

        // Alarm clock style sizes - balanced for visibility without being too large
        // Platform-specific adjustments
        var platform = DeviceInfo.Platform;
        bool isAndroid = platform == DevicePlatform.Android;

        // Android-specific reduction factor for alarm fonts (reduce by ~25%)
        double androidAlarmReduction = isAndroid ? 0.75 : 1.0;

        // Prominent time display (alarm clock style)
        double baseAlarmTimeSize = 32.0 * androidAlarmReduction;
        // Smaller but still prominent AM/PM
        double baseAlarmMeridianSize = 18.0 * androidAlarmReduction;
        // Large bell icon (4x TitleFontSize of 20pt)
        const double baseAlarmBellIconSize = 80.0;

        if (!hasValidDisplayInfo)
        {
            // Fallback to Windows-specific fixed sizes or platform detection
            var deviceIdiom = DeviceInfo.Idiom;

            if (platform == DevicePlatform.WinUI || deviceIdiom == DeviceIdiom.Desktop)
            {
                // Windows desktop: Use fixed sizes for desktop readability (same as before width-in-dp change)
                // These are larger than mobile for better desktop readability
                _standardFontSize = 14.0;
                _headerFontSize = 20.0;
                _smallFontSize = 12.0;
                _mediumFontSize = 16.0;
                _largeFontSize = 18.0;
                _titleFontSize = 22.0;

                // Alarm fonts - fixed sizes for desktop
                _alarmTimeFontSize = 30.0;
                _alarmMeridianFontSize = 16.0;
                // Fixed size for desktop (4x TitleFontSize)
                _alarmBellIconFontSize = 80.0;

                Log.Logger.Debug("Using Windows desktop fallback fixed font sizes (display info not available)");
            }
            else
            {
                // Other platforms: Use base sizes directly (no scaling when display info is invalid)
                // This is a fallback - when display info becomes available, it will recalculate
                _standardFontSize = baseStandardSize;
                _headerFontSize = baseHeaderSize;
                _smallFontSize = baseSmallSize;
                _mediumFontSize = baseMediumSize;
                _largeFontSize = baseLargeSize;
                _titleFontSize = baseTitleSize;

                // Alarm fonts - use base sizes with reasonable caps (Android gets smaller)
                double fallbackMaxTime = isAndroid ? 30.0 : 40.0;
                double fallbackMaxMeridian = isAndroid ? 16.0 : 22.0;
                _alarmTimeFontSize = Math.Min(baseAlarmTimeSize, fallbackMaxTime);
                _alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize, fallbackMaxMeridian);
                _alarmBellIconFontSize = Math.Min(baseAlarmBellIconSize, 100.0);

                Log.Logger.Warning("Invalid display info detected on non-Windows platform, using base font sizes as fallback");
            }
        }
        else
        {
            // Valid display info - use width-in-dp based scaling
            density = mainDisplayInfo.Density;
            // Logical dp - key to proper scaling
            widthDp = mainDisplayInfo.Width / density;

            // Determine scaling factor based on screen width in dp (density-independent pixels)
            // This approach works perfectly for phones, tablets, foldables, and resizable desktop windows
            double scale;
            // Phone (portrait or landscape)
            bool isPhone = widthDp < 600;
            // Tablet or small desktop window
            bool isTablet = widthDp >= 600 && widthDp < 960;

            if (isPhone)
            {
                // Phone: Conservative scaling for smaller screens
                scale = Math.Min(density, 1.5);
            }
            else if (isTablet)
            {
                // Tablet: Moderate scaling - more screen real estate
                scale = Math.Min(density, 1.8);
            }
            else
            {
                // Desktop: Allow more scaling for large screens, but still cap it
                // On real desktop, use slightly larger base or allow more density scaling
                scale = Math.Min(density, 2.2);
            }

            // Calculate font sizes with appropriate caps
            // Standard fonts
            _standardFontSize = Math.Min(baseStandardSize * scale, 17.0);
            _headerFontSize = Math.Min(baseHeaderSize * scale, 26.0);
            _smallFontSize = Math.Min(baseSmallSize * scale, 14.0);
            _mediumFontSize = Math.Min(baseMediumSize * scale, 19.0);
            _largeFontSize = Math.Min(baseLargeSize * scale, 22.0);
            _titleFontSize = Math.Min(baseTitleSize * scale, 30.0);

            // Alarm fonts - different caps based on screen size and platform
            // Android gets smaller max caps
            double maxAlarmTimeSize = isPhone
                ? (isAndroid ? 30.0 : 40.0)
                : (isAndroid ? 38.0 : 50.0);
            double maxAlarmMeridianSize = isPhone
                ? (isAndroid ? 16.0 : 22.0)
                : (isAndroid ? 21.0 : 28.0);
            double maxAlarmBellIconSize = isPhone ? 100.0 : 130.0;

            _alarmTimeFontSize = Math.Min(baseAlarmTimeSize * scale, maxAlarmTimeSize);
            _alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * scale, maxAlarmMeridianSize);
            _alarmBellIconFontSize = Math.Min(baseAlarmBellIconSize * scale, maxAlarmBellIconSize);
        }

        // Button font size is 1 point smaller than HeaderFontSize
        _buttonFontSize = _headerFontSize - 1.0;

        // Notify all bindings that font sizes have changed
        RaiseAllPropertiesChanged();
    }

    private void RaiseAllPropertiesChanged()
    {
        // Notify that all properties changed - forces all bindings to re-evaluate
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Unsubscribe from display info changes
        DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;

        // No injected services to dispose (this service has no dependencies)
    }

    // Properties (not fields) so bindings work correctly and PropertyChanged can fire
    public double StandardFontSize => _standardFontSize;
    public double HeaderFontSize => _headerFontSize;
    public double ButtonFontSize => _buttonFontSize;
    public double SmallFontSize => _smallFontSize;
    public double MediumFontSize => _mediumFontSize;
    public double LargeFontSize => _largeFontSize;
    public double TitleFontSize => _titleFontSize;
    public double AlarmTimeFontSize => _alarmTimeFontSize;
    public double AlarmMeridianFontSize => _alarmMeridianFontSize;
    public double AlarmBellIconFontSize => _alarmBellIconFontSize;

    public double GetScaledFontSize(double baseSizeInPoints)
    {
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

        // Handle invalid display info
        double density = mainDisplayInfo.Density > 0 ? mainDisplayInfo.Density : 1.0;

        return baseSizeInPoints * Math.Min(density, 2.0);
    }
}

