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

        // Scale by density
        _standardFontSize = baseStandardSize * _density;
        _headerFontSize = baseHeaderSize * _density;
        _smallFontSize = baseSmallSize * _density;
        _mediumFontSize = baseMediumSize * _density;
        _largeFontSize = baseLargeSize * _density;
        _titleFontSize = baseTitleSize * _density;
    }

    public double StandardFontSize => _standardFontSize;
    public double HeaderFontSize => _headerFontSize;
    public double SmallFontSize => _smallFontSize;
    public double MediumFontSize => _mediumFontSize;
    public double LargeFontSize => _largeFontSize;
    public double TitleFontSize => _titleFontSize;

    public double GetScaledFontSize(double baseSizeInPoints)
    {
        return baseSizeInPoints * _density;
    }
}

