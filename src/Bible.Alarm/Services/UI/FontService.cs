#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;
using System.ComponentModel;
using Serilog;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Service for managing scalable font sizes based on device metrics.
/// Uses screen width in density-independent pixels (dp) to determine device category,
/// then applies density scaling with appropriate caps for optimal readability.
/// Automatically recalculates when screen size/orientation changes.
/// </summary>
public class FontService : IFontService, INotifyPropertyChanged
{
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
        
#if DEBUG
        // Enable Hot Reload support - font sizes will update when XAML changes
        try
        {
            var hotReloadType = Type.GetType("Microsoft.Maui.HotReload.MauiHotReloadHelper, Microsoft.Maui.Controls");
            if (hotReloadType != null)
            {
                var registerMethod = hotReloadType.GetMethod("Register", new[] { typeof(object), typeof(object) });
                registerMethod?.Invoke(null, new[] { this, this });
            }
        }
        catch (Exception ex)
        {
            // Hot Reload helper not available - continue without it
            Log.Logger.Debug(ex, "Hot Reload helper not available, continuing without it");
        }
#endif
        
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
        // Prominent time display (alarm clock style)
        const double baseAlarmTimeSize = 32.0;
        // Smaller but still prominent AM/PM
        const double baseAlarmMeridianSize = 18.0;
        // Large bell icon (4x TitleFontSize of 20pt)
        const double baseAlarmBellIconSize = 80.0;

        if (!hasValidDisplayInfo)
        {
            // Fallback to Windows-specific fixed sizes or platform detection
            var platform = DeviceInfo.Platform;
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
                
                // Alarm fonts - use base sizes with reasonable caps
                _alarmTimeFontSize = Math.Min(baseAlarmTimeSize, 40.0);
                _alarmMeridianFontSize = Math.Min(baseAlarmMeridianSize, 22.0);
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
            // Large desktop, landscape tablet in full screen
            bool isDesktop = widthDp >= 960;
            
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
            
            // Alarm fonts - different caps based on screen size
            double maxAlarmTimeSize = isPhone ? 40.0 : 50.0;
            double maxAlarmMeridianSize = isPhone ? 22.0 : 28.0;
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

