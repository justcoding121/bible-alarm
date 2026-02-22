#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Resolves localized language names by display language code (e.g. "E" for English).
/// Supports in-memory cache warmed at app start for the current app language ("E").
/// </summary>
public interface ILanguageNameService
{
    /// <summary>
    /// Loads language code/id → display name for the given language into memory for fast lookup.
    /// Call once at app start for the current app language (e.g. "E").
    /// </summary>
    Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name for a language in the given display language code.
    /// Returns null if not found. Use languageCode as fallback for display.
    /// </summary>
    Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name for a language (by its LanguageCode) in the given display language code.
    /// Returns null if not found.
    /// </summary>
    Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets names for multiple language IDs in one query for the given display language code.
    /// Returns a dictionary languageId -> name; missing entries mean no name found (use code as fallback).
    /// </summary>
    Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the display name from the in-memory cache only (no DB). Use after WarmCacheForDisplayLanguageAsync("E").
    /// Returns null if not in cache. Use languageCode as fallback for display.
    /// </summary>
    string? GetNameCached(int languageId);

    /// <summary>
    /// Gets the display name by language code from the in-memory cache only (no DB).
    /// Returns null if not in cache.
    /// </summary>
    string? GetNameByLanguageCodeCached(string languageCode);
}
