#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Service for managing scalable font sizes based on device metrics.
/// Uses DeviceDisplay.MainDisplayInfo.Density to scale font sizes appropriately.
/// </summary>
public class FontService : IFontService
{
    private readonly double _density;
    private readonly double _standardFontSize;
    private readonly double _headerFontSize;
    private readonly double _buttonFontSize;
    private readonly double _smallFontSize;
    private readonly double _mediumFontSize;
    private readonly double _largeFontSize;
    private readonly double _titleFontSize;
    private readonly double _alarmTimeFontSize;
    private readonly double _alarmMeridianFontSize;
    private readonly double _alarmBellIconFontSize;

    public FontService()
    {
        // Get device density for scaling
        var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
        _density = mainDisplayInfo.Density;

        // Base sizes in points (standard practice: 12pt base)
        const double baseStandardSize = 12.0;
        const double baseHeaderSize = 18.0;
        const double baseSmallSize = 10.0;
        const double baseMediumSize = 14.0;
        const double baseLargeSize = 16.0;
        const double baseTitleSize = 20.0;
        
        // Alarm clock style sizes - balanced for visibility without being too large
        // Sizes that are noticeably larger than standard but won't push list height excessively
        const double baseAlarmTimeSize = 32.0;      // Prominent time display (alarm clock style)
        const double baseAlarmMeridianSize = 18.0;   // Smaller but still prominent AM/PM
        const double baseAlarmBellIconSize = 80.0;   // Large bell icon (4x TitleFontSize of 20pt)

        // Platform and device type specific handling
        // Use DeviceInfo.Idiom to distinguish between Phone, Tablet, and Desktop
        var deviceIdiom = DeviceInfo.Idiom;
        var platform = DeviceInfo.Platform;
        
        if (platform == DevicePlatform.WinUI || deviceIdiom == DeviceIdiom.Desktop)
        {
            // Desktop/Windows: Use larger fixed sizes for better readability on desktop
            _standardFontSize = 14.0;  // Larger than mobile for desktop readability
            _headerFontSize = 20.0;    // Proportionally larger
            _smallFontSize = 12.0;     // Proportionally larger
            _mediumFontSize = 16.0;    // Proportionally larger
            _largeFontSize = 18.0;     // Proportionally larger
            _titleFontSize = 22.0;     // Proportionally larger
            
            // Alarm fonts - fixed sizes for desktop
            _alarmTimeFontSize = 30.0;  // Fixed size for desktop
            _alarmMeridianFontSize = 16.0;  // Fixed size for desktop
            _alarmBellIconFontSize = 80.0;  // Fixed size for desktop (4x TitleFontSize)
        }
        else if (deviceIdiom == DeviceIdiom.Tablet)
        {
            // Tablet: Use moderate scaling - tablets have larger screens but still mobile-like density
            // Allow slightly more scaling than phones since tablets have more screen real estate
            double tabletScalingFactor = Math.Min(_density, 1.8);  // Cap scaling at 1.8x for tablets
            
            _standardFontSize = baseStandardSize * tabletScalingFactor;
            _headerFontSize = baseHeaderSize * tabletScalingFactor;
            _smallFontSize = baseSmallSize * tabletScalingFactor;
            _mediumFontSize = baseMediumSize * tabletScalingFactor;
            _largeFontSize = baseLargeSize * tabletScalingFactor;
            _titleFontSize = baseTitleSize * tabletScalingFactor;
            
            // Alarm fonts - use moderate scaling for tablets
            const double maxAlarmTimeSize = 45.0;  // Cap at 45pt for tablets (slightly larger than phones)
            const double maxAlarmMeridianSize = 24.0;  // Cap at 24pt for tablets
            const double maxAlarmBellIconSize = 110.0;  // Cap at 110pt for tablets
            
            _alarmTimeFontSize = Math.Min(baseAlarmTimeSize * tabletScalingFactor, maxAlarmTimeSize);
            _alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * tabletScalingFactor, maxAlarmMeridianSize);
            _alarmBellIconFontSize = Math.Min(baseAlarmBellIconSize * tabletScalingFactor, maxAlarmBellIconSize);
        }
        else
        {
            // Phone: Use conservative scaling - phones have smaller screens and high density
            // On phones, density is often 2.0-3.0, which makes fonts too large
            // Use a scaling factor that's more reasonable for phone screens
            double phoneScalingFactor = Math.Min(_density, 1.5);  // Cap scaling at 1.5x for phones
            
            _standardFontSize = baseStandardSize * phoneScalingFactor;
            _headerFontSize = baseHeaderSize * phoneScalingFactor;
            _smallFontSize = baseSmallSize * phoneScalingFactor;
            _mediumFontSize = baseMediumSize * phoneScalingFactor;
            _largeFontSize = baseLargeSize * phoneScalingFactor;
            _titleFontSize = baseTitleSize * phoneScalingFactor;
            
            // Alarm fonts - use reduced scaling on phones to prevent them from being too large
            const double maxAlarmTimeSize = 40.0;  // Cap at 40pt for phones
            const double maxAlarmMeridianSize = 22.0;  // Cap at 22pt for phones
            const double maxAlarmBellIconSize = 100.0;  // Cap at 100pt for phones
            
            _alarmTimeFontSize = Math.Min(baseAlarmTimeSize * phoneScalingFactor, maxAlarmTimeSize);
            _alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize * phoneScalingFactor, maxAlarmMeridianSize);
            _alarmBellIconFontSize = Math.Min(baseAlarmBellIconSize * phoneScalingFactor, maxAlarmBellIconSize);
        }
        
        // Button font size is 1 point smaller than HeaderFontSize on all platforms
        _buttonFontSize = _headerFontSize - 1.0;
    }

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
        return baseSizeInPoints * _density;
    }
}

