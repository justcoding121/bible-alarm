#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Resolves localized category names from embedded JSON resources (e.g. Resources/CategoryNames/E.json).
/// Caches category code → name for the current app language in memory after WarmCacheForDisplayLanguageAsync.
/// </summary>
public sealed class CategoryNameService(ILogger logger) : ICategoryNameService
{
    private const string ResourceNamePrefix = "Bible.Alarm.Shared.Resources.CategoryNames.";

    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly object cacheLock = new();
    private string? warmedDisplayLanguageCode;
    private Dictionary<string, string>? cacheByCategoryCode;

    public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayLanguageCode))
        {
            return Task.CompletedTask;
        }

        var dict = LoadCategoryNamesForLanguage(displayLanguageCode);

        lock (cacheLock)
        {
            warmedDisplayLanguageCode = displayLanguageCode;
            cacheByCategoryCode = dict;
        }

        logger.Information("CategoryNameService: Warmed in-memory cache for display language {DisplayLanguageCode} with {Count} category names",
            displayLanguageCode, dict.Count);

        return Task.CompletedTask;
    }

    public string? GetName(string categoryCode, string displayLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            return null;
        }

        lock (cacheLock)
        {
            if (cacheByCategoryCode != null &&
                string.Equals(warmedDisplayLanguageCode, displayLanguageCode, StringComparison.OrdinalIgnoreCase) &&
                cacheByCategoryCode.TryGetValue(categoryCode, out var name))
            {
                return name;
            }
        }

        return null;
    }

    private static Dictionary<string, string> LoadCategoryNamesForLanguage(string languageCode)
    {
        var assembly = typeof(CategoryNameService).Assembly;
        var resourceName = ResourceNamePrefix + languageCode + ".json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict == null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in dict.Where(k => !string.IsNullOrEmpty(k.Key)))
        {
            result[kvp.Key] = kvp.Value ?? kvp.Key;
        }

        return result;
    }
}
