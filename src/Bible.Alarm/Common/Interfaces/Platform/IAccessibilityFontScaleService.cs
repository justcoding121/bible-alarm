#nullable enable

namespace Bible.Alarm.Common.Interfaces.Platform;

/// <summary>
/// Service for detecting the operating system's accessibility font scale setting.
/// This allows the app to respect user preferences for larger text.
/// </summary>
public interface IAccessibilityFontScaleService
{
    /// <summary>
    /// Gets the current OS font scale factor.
    /// A value of 1.0 means default/normal size.
    /// Values greater than 1.0 indicate the user has increased text size for accessibility.
    /// Values less than 1.0 indicate smaller text preference.
    /// </summary>
    double FontScale { get; }

    /// <summary>
    /// Event raised when the font scale changes (e.g., user changes accessibility settings).
    /// </summary>
    event EventHandler<double>? FontScaleChanged;
}
