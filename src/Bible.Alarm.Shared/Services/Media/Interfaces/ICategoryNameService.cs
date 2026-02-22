#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Resolves localized category names by display language code (e.g. "E" for English).
/// Supports in-memory cache warmed at app start for the current app language ("E").
/// </summary>
public interface ICategoryNameService
{
    /// <summary>
    /// Loads category code → display name for the given language into memory for fast lookup.
    /// Call once at app start for the current app language (e.g. "E").
    /// </summary>
    Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the display name for a category by its code. Uses in-memory cache when warmed for the given language.
    /// Returns null if not found. Use categoryCode as fallback for display.
    /// </summary>
    string? GetName(string categoryCode, string displayLanguageCode);
}
