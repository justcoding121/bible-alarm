#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Resolves localized category names from CategoryNamesByLanguage.
/// Caches category code → name for the current app language ("E") in memory after WarmCacheForDisplayLanguageAsync.
/// </summary>
public sealed class CategoryNameService(IServiceScopeFactory scopeFactory, ILogger logger) : ICategoryNameService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly object cacheLock = new();
    private string? warmedDisplayLanguageCode;
    private Dictionary<string, string>? cacheByCategoryCode;

    public async Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayLanguageCode))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var pairs = await db.CategoryNamesByLanguage
            .AsNoTracking()
            .Where(cnl => cnl.LanguageCode == displayLanguageCode)
            .Join(db.Categories.AsNoTracking(),
                cnl => cnl.CategoryId,
                c => c.Id,
                (cnl, c) => new { c.CategoryCode, cnl.Name })
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pairs)
        {
            if (!string.IsNullOrEmpty(p.CategoryCode))
            {
                dict[p.CategoryCode] = p.Name ?? p.CategoryCode;
            }
        }

        lock (cacheLock)
        {
            warmedDisplayLanguageCode = displayLanguageCode;
            cacheByCategoryCode = dict;
        }

        logger.Information("CategoryNameService: Warmed in-memory cache for display language {DisplayLanguageCode} with {Count} category names",
            displayLanguageCode, dict.Count);
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
}
