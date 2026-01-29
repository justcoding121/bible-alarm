#nullable enable

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Platform-specific font size defaults based on industry standards
/// </summary>
internal record PlatformFontDefaults(
    double StandardSize,    // Body text
    double HeaderSize,       // Headers
    double SmallSize,       // Small text / Caption
    double SmallMediumSize, // Small-medium text
    double MediumSize,      // Medium text
    double LargeSize,       // Large text
    double TitleSize,       // Title / Display text
    double AlarmTimeSize,   // Alarm time in list items (per alarm clock app standards)
    double AlarmMeridianSize // AM/PM in list items
)
{
    /// <summary>
    /// iOS defaults per Apple Human Interface Guidelines
    /// Body: 17pt, Caption: 13pt, Headline: 22pt, Title: 28pt
    /// Alarm time: 36pt for prominent display (per Clock app standards for list items)
    /// </summary>
    public static PlatformFontDefaults iOS { get; } = new(
        StandardSize: 17.0,    // iOS Body (17pt)
        HeaderSize: 22.0,      // iOS Headline (22pt)
        SmallSize: 13.0,       // iOS Caption (13pt)
        SmallMediumSize: 15.0, // iOS Subhead (15pt)
        MediumSize: 17.0,      // iOS Body (17pt)
        LargeSize: 22.0,      // iOS Headline (22pt)
        TitleSize: 28.0,       // iOS Title 1 (28pt)
        AlarmTimeSize: 36.0,   // iOS alarm time (36pt, prominent display size)
        AlarmMeridianSize: 18.0 // iOS AM/PM (18pt, Headline size)
    );

    /// <summary>
    /// Android defaults per Material Design typography scale
    /// Body Large: 16sp, Body Medium: 14sp, Body Small: 12sp, Headline: 20-24sp
    /// Alarm time: 28sp for prominent display (per Material Design and alarm app standards)
    /// </summary>
    public static PlatformFontDefaults Android { get; } = new(
        StandardSize: 16.0,    // Android Body Large (16sp)
        HeaderSize: 20.0,     // Android Headline Medium (20sp)
        SmallSize: 12.0,      // Android Body Small (12sp)
        SmallMediumSize: 14.0, // Android Body Medium (14sp)
        MediumSize: 16.0,     // Android Body Large (16sp)
        LargeSize: 20.0,      // Android Headline Medium (20sp)
        TitleSize: 24.0,       // Android Headline Large (24sp)
        AlarmTimeSize: 28.0,   // Android alarm time (28sp, prominent display size)
        AlarmMeridianSize: 18.0 // Android AM/PM (18sp, Headline Medium size)
    );

    /// <summary>
    /// Windows defaults per Fluent Design System
    /// Body: 14pt (desktop standard), Header: 18pt, Title: 24pt
    /// Alarm time: 32pt for prominent display (larger for desktop readability)
    /// </summary>
    public static PlatformFontDefaults Windows { get; } = new(
        StandardSize: 14.0,    // Windows Body (14pt)
        HeaderSize: 18.0,      // Windows Header (18pt)
        SmallSize: 12.0,       // Windows Caption (12pt)
        SmallMediumSize: 13.0, // Windows Small (13pt)
        MediumSize: 14.0,      // Windows Body (14pt)
        LargeSize: 18.0,       // Windows Header (18pt)
        TitleSize: 24.0,       // Windows Title (24pt)
        AlarmTimeSize: 32.0,   // Windows alarm time (32pt, prominent display size)
        AlarmMeridianSize: 18.0 // Windows AM/PM (18pt, matches Header)
    );
}

