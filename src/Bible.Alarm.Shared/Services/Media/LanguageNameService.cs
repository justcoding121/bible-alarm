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
/// Resolves localized language names from LanguageNamesByLanguage table.
/// Caches language code/id → name for the current app language ("E") in memory after WarmCacheForDisplayLanguageAsync.
/// </summary>
public sealed class LanguageNameService(IServiceScopeFactory scopeFactory, ILogger logger) : ILanguageNameService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly object cacheLock = new();
    private string? warmedDisplayLanguageCode;
    private Dictionary<int, string>? cacheById;
    private Dictionary<string, string>? cacheByCode;

    public async Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayLanguageCode))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var pairs = await db.LanguageNamesByLanguage
            .AsNoTracking()
            .Where(x => x.DisplayLanguageCode == displayLanguageCode)
            .Join(db.Languages.AsNoTracking(),
                lnl => lnl.LanguageId,
                l => l.Id,
                (lnl, l) => new { lnl.LanguageId, l.LanguageCode, lnl.Name })
            .ToListAsync(cancellationToken);

        var byId = new Dictionary<int, string>();
        var byCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pairs)
        {
            var name = p.Name ?? p.LanguageCode ?? "";
            byId[p.LanguageId] = name;
            if (!string.IsNullOrEmpty(p.LanguageCode))
            {
                byCode[p.LanguageCode] = name;
            }
        }

        lock (cacheLock)
        {
            warmedDisplayLanguageCode = displayLanguageCode;
            cacheById = byId;
            cacheByCode = byCode;
        }

        logger.Information("LanguageNameService: Warmed in-memory cache for display language {DisplayLanguageCode} with {Count} language names",
            displayLanguageCode, byId.Count);
    }

    public async Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayLanguageCode))
        {
            return null;
        }

        lock (cacheLock)
        {
            if (cacheById != null &&
                string.Equals(warmedDisplayLanguageCode, displayLanguageCode, StringComparison.OrdinalIgnoreCase) &&
                cacheById.TryGetValue(languageId, out var name))
            {
                return name;
            }
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var entry = await db.LanguageNamesByLanguage
            .AsNoTracking()
            .Where(x => x.LanguageId == languageId && x.DisplayLanguageCode == displayLanguageCode)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return entry;
    }

    public async Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || string.IsNullOrWhiteSpace(displayLanguageCode))
        {
            return null;
        }

        lock (cacheLock)
        {
            if (cacheByCode != null &&
                string.Equals(warmedDisplayLanguageCode, displayLanguageCode, StringComparison.OrdinalIgnoreCase) &&
                cacheByCode.TryGetValue(languageCode, out var name))
            {
                return name;
            }
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var dbName = await db.LanguageNamesByLanguage
            .AsNoTracking()
            .Where(x => x.Language!.LanguageCode == languageCode && x.DisplayLanguageCode == displayLanguageCode)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return dbName;
    }

    public async Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default)
    {
        var idList = languageIds.ToList();
        if (idList.Count == 0 || string.IsNullOrWhiteSpace(displayLanguageCode))
        {
            return new Dictionary<int, string>();
        }

        lock (cacheLock)
        {
            if (cacheById != null &&
                string.Equals(warmedDisplayLanguageCode, displayLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                var result = new Dictionary<int, string>();
                foreach (var id in idList)
                {
                    if (cacheById.TryGetValue(id, out var name))
                    {
                        result[id] = name;
                    }
                }
                return result;
            }
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var pairs = await db.LanguageNamesByLanguage
            .AsNoTracking()
            .Where(x => idList.Contains(x.LanguageId) && x.DisplayLanguageCode == displayLanguageCode)
            .Select(x => new { x.LanguageId, x.Name })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(x => x.LanguageId, x => x.Name, null);
    }

    public string? GetNameCached(int languageId)
    {
        lock (cacheLock)
        {
            if (cacheById != null && cacheById.TryGetValue(languageId, out var name))
            {
                return name;
            }
        }
        return null;
    }

    public string? GetNameByLanguageCodeCached(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }
        lock (cacheLock)
        {
            if (cacheByCode != null && cacheByCode.TryGetValue(languageCode, out var name))
            {
                return name;
            }
        }
        return null;
    }
}
