#nullable enable

using Bible.Alarm.Common.Interfaces.Platform;

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Fallback implementation of IAccessibilityFontScaleService that returns default scale (1.0).
/// Used only for hot reload compatibility when the real service isn't yet initialized.
/// </summary>
internal sealed class FallbackAccessibilityFontScaleService : IAccessibilityFontScaleService
{
    public double FontScale => 1.0;
#pragma warning disable CS0067 // Event is never used - required by interface
    public event EventHandler<double>? FontScaleChanged;
#pragma warning restore CS0067
}
