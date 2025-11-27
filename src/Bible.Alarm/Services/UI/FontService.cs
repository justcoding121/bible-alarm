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

        // Platform-specific handling
        // On Windows, density is often 1.0, so we need larger base sizes for readability
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            // Use larger fixed sizes on Windows for better readability on desktop
            _standardFontSize = 14.0;  // Larger than mobile for desktop readability
            _headerFontSize = 20.0;    // Proportionally larger
            _smallFontSize = 12.0;     // Proportionally larger
            _mediumFontSize = 16.0;    // Proportionally larger
            _largeFontSize = 18.0;     // Proportionally larger
            _titleFontSize = 22.0;     // Proportionally larger
            
            // Alarm fonts - fixed sizes for Windows
            _alarmTimeFontSize = 30.0;  // Fixed size for Windows
            _alarmMeridianFontSize = 16.0;  // Fixed size for Windows
            _alarmBellIconFontSize = 80.0;  // Fixed size for Windows (4x TitleFontSize)
        }
        else
        {
            // Use density scaling on mobile platforms
            _standardFontSize = baseStandardSize * _density;
            _headerFontSize = baseHeaderSize * _density;
            _smallFontSize = baseSmallSize * _density;
            _mediumFontSize = baseMediumSize * _density;
            _largeFontSize = baseLargeSize * _density;
            _titleFontSize = baseTitleSize * _density;
            
            // Alarm fonts - use density scaling on mobile platforms
            _alarmTimeFontSize = baseAlarmTimeSize * _density;
            _alarmMeridianFontSize = baseAlarmMeridianSize * _density;
            _alarmBellIconFontSize = baseAlarmBellIconSize * _density;
        }
    }

    public double StandardFontSize => _standardFontSize;
    public double HeaderFontSize => _headerFontSize;
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

