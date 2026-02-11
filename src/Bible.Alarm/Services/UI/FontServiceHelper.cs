#nullable enable
using Bible.Alarm.Services.UI.Interfaces;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Static helper class to expose FontService properties for XAML binding via x:Static
/// Hot-reload friendly: Properties always return valid values even if service isn't initialized
/// </summary>
public static class FontServiceHelper
{
    private static IFontService? fontService;
    private static readonly Lock @lock = new();

    /// <summary>
    /// Initializes the FontServiceHelper with the service instance.
    /// This should be called during app startup after services are registered.
    /// </summary>
    public static void Initialize(IFontService fontService)
    {
        lock (@lock)
        {
            FontServiceHelper.fontService = fontService;
        }
    }

    /// <summary>
    /// Gets the font service instance, creating a temporary one if needed for hot reload compatibility
    /// </summary>
    internal static IFontService GetFontService()
    {
        if (fontService != null)
        {
            return fontService;
        }

        // For hot reload compatibility: create a temporary service if not initialized
        // This ensures x:Static bindings always work even during hot reload
        lock (@lock)
        {
            if (fontService == null)
            {
                // Create with a fallback accessibility service for hot reload scenarios
                fontService = new FontService(new FallbackAccessibilityFontScaleService());
            }
            return fontService;
        }
    }

    /// <summary>
    /// Gets the standard font size (platform-specific: iOS 17pt, Android 16sp, Windows 14pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double StandardFontSize => GetFontService().StandardFontSize;

    /// <summary>
    /// Gets the header font size (platform-specific: iOS 22pt, Android 20sp, Windows 18pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double HeaderFontSize => GetFontService().HeaderFontSize;

    /// <summary>
    /// Gets the button font size (HeaderFontSize - 1pt) for consistent button text sizing
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double ButtonFontSize => GetFontService().ButtonFontSize;

    /// <summary>
    /// Gets the small font size (platform-specific: iOS 13pt, Android 12sp, Windows 12pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double SmallFontSize => GetFontService().SmallFontSize;

    /// <summary>
    /// Gets the small-medium font size (platform-specific: iOS 15pt, Android 14sp, Windows 13pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double SmallMediumFontSize => GetFontService().SmallMediumFontSize;

    /// <summary>
    /// Gets the medium font size (platform-specific: iOS 17pt, Android 16sp, Windows 14pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double MediumFontSize => GetFontService().MediumFontSize;

    /// <summary>
    /// Gets the large font size (platform-specific: iOS 22pt, Android 20sp, Windows 18pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double LargeFontSize => GetFontService().LargeFontSize;

    /// <summary>
    /// Gets the title font size (platform-specific: iOS 28pt, Android 24sp, Windows 24pt) scaled by density
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double TitleFontSize => GetFontService().TitleFontSize;

    /// <summary>
    /// Gets the alarm time font size (32pt base) - for prominent time display in alarm clock style
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double AlarmTimeFontSize => GetFontService().AlarmTimeFontSize;

    /// <summary>
    /// Gets the alarm meridian font size (18pt base) - for AM/PM display in alarm clock style
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double AlarmMeridianFontSize => GetFontService().AlarmMeridianFontSize;

    /// <summary>
    /// Gets the alarm bell icon font size (80pt base, 4x TitleFontSize) - for large bell icon in alarm modal
    /// Hot-reload friendly: Always returns a valid value
    /// </summary>
    public static double AlarmBellIconFontSize => GetFontService().AlarmBellIconFontSize;
}

