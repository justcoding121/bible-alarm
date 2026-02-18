namespace Bible.Alarm.Services.UI.Interfaces;

/// <summary>
/// Service for managing scalable font sizes based on device metrics and OS accessibility settings
/// </summary>
public interface IFontService : IDisposable
{
    /// <summary>
    /// Base font size (platform-specific: iOS 17pt, Android 16sp, Windows 14pt) scaled by device density and OS accessibility settings
    /// </summary>
    double StandardFontSize { get; }

    /// <summary>
    /// Header font size (platform-specific: iOS 22pt, Android 20sp, Windows 18pt) scaled by device density and OS accessibility settings
    /// </summary>
    double HeaderFontSize { get; }

    /// <summary>
    /// Button font size (HeaderFontSize - 1 point) for consistent button text sizing
    /// </summary>
    double ButtonFontSize { get; }

    /// <summary>
    /// Small font size (platform-specific: iOS 13pt, Android 12sp, Windows 12pt) scaled by device density and OS accessibility settings
    /// </summary>
    double SmallFontSize { get; }

    /// <summary>
    /// Small-medium font size (platform-specific: iOS 15pt, Android 14sp, Windows 13pt) scaled by device density and OS accessibility settings
    /// </summary>
    double SmallMediumFontSize { get; }

    /// <summary>
    /// Medium font size (platform-specific: iOS 17pt, Android 16sp, Windows 14pt) scaled by device density and OS accessibility settings
    /// </summary>
    double MediumFontSize { get; }

    /// <summary>
    /// Large font size (platform-specific: iOS 22pt, Android 20sp, Windows 18pt) scaled by device density and OS accessibility settings
    /// </summary>
    double LargeFontSize { get; }

    /// <summary>
    /// Title font size (platform-specific: iOS 28pt, Android 24sp, Windows 24pt) scaled by device density and OS accessibility settings
    /// </summary>
    double TitleFontSize { get; }

    /// <summary>
    /// Alarm time font size (32pt base) scaled by device density and OS accessibility settings
    /// </summary>
    double AlarmTimeFontSize { get; }

    /// <summary>
    /// Alarm meridian font size (18pt base) scaled by device density and OS accessibility settings
    /// </summary>
    double AlarmMeridianFontSize { get; }

    /// <summary>
    /// Alarm bell icon font size (80pt base) scaled by device density and OS accessibility settings
    /// </summary>
    double AlarmBellIconFontSize { get; }

    /// <summary>
    /// Small icon font size (14pt) for small icon fonts, scaled by accessibility settings
    /// </summary>
    double IconSmallFontSize { get; }

    /// <summary>
    /// Standard icon font size (20pt) for normal icon fonts, scaled by accessibility settings
    /// </summary>
    double IconStandardFontSize { get; }

    /// <summary>
    /// Large icon font size (28pt) for larger icon fonts, scaled by accessibility settings
    /// </summary>
    double IconLargeFontSize { get; }

    /// <summary>
    /// Small icon container size (32pt base) for small icon button containers, scaled by accessibility settings
    /// </summary>
    double IconSmallContainerSize { get; }

    /// <summary>
    /// Standard icon container size (36-48pt base) for standard icon button containers, scaled by accessibility settings
    /// </summary>
    double IconStandardContainerSize { get; }

    /// <summary>
    /// Large icon container size (56pt base) for large icon button containers like play button, scaled by accessibility settings
    /// </summary>
    double IconLargeContainerSize { get; }

    /// <summary>
    /// Small content width (200pt base) for content elements like progress bars, scaled by accessibility settings
    /// </summary>
    double ContentWidthSmall { get; }

    /// <summary>
    /// Medium content width (240pt base) for content elements like progress indicator grids, scaled by accessibility settings
    /// </summary>
    double ContentWidthMedium { get; }

    /// <summary>
    /// Large content width (280pt base) for content elements like modal borders and artwork containers, scaled by accessibility settings
    /// </summary>
    double ContentWidthLarge { get; }

    /// <summary>
    /// Spinner (ActivityIndicator) container size. Larger on iOS for visibility; same as IconStandardContainerSize elsewhere.
    /// </summary>
    double SpinnerContainerSize { get; }

    /// <summary>
    /// Spinner (ActivityIndicator) size for small spinners. Larger on iOS; same as IconStandardFontSize elsewhere.
    /// </summary>
    double SpinnerFontSize { get; }

    /// <summary>
    /// Gets a scaled font size based on a base size in points
    /// </summary>
    double GetScaledFontSize(double baseSizeInPoints);
}

