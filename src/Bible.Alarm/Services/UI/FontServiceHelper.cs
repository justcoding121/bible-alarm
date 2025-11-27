#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Static helper class to expose FontService properties for XAML binding via x:Static
/// </summary>
public static class FontServiceHelper
{
    private static IFontService? _fontService;

    /// <summary>
    /// Initializes the FontServiceHelper with the service instance.
    /// This should be called during app startup after services are registered.
    /// </summary>
    public static void Initialize(IFontService fontService)
    {
        _fontService = fontService;
    }

    /// <summary>
    /// Gets the standard font size (12pt scaled by density)
    /// </summary>
    public static double StandardFontSize => _fontService?.StandardFontSize ?? 12.0;

    /// <summary>
    /// Gets the header font size (18pt scaled by density)
    /// </summary>
    public static double HeaderFontSize => _fontService?.HeaderFontSize ?? 18.0;

    /// <summary>
    /// Gets the small font size (10pt scaled by density)
    /// </summary>
    public static double SmallFontSize => _fontService?.SmallFontSize ?? 10.0;

    /// <summary>
    /// Gets the medium font size (14pt scaled by density)
    /// </summary>
    public static double MediumFontSize => _fontService?.MediumFontSize ?? 14.0;

    /// <summary>
    /// Gets the large font size (16pt scaled by density)
    /// </summary>
    public static double LargeFontSize => _fontService?.LargeFontSize ?? 16.0;

    /// <summary>
    /// Gets the title font size (20pt scaled by density)
    /// </summary>
    public static double TitleFontSize => _fontService?.TitleFontSize ?? 20.0;

    /// <summary>
    /// Gets the alarm time font size (32pt base) - for prominent time display in alarm clock style
    /// </summary>
    public static double AlarmTimeFontSize => _fontService?.AlarmTimeFontSize ?? 32.0;

    /// <summary>
    /// Gets the alarm meridian font size (18pt base) - for AM/PM display in alarm clock style
    /// </summary>
    public static double AlarmMeridianFontSize => _fontService?.AlarmMeridianFontSize ?? 18.0;
}

