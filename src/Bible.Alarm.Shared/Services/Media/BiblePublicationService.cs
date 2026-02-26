#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BiblePublication database operations.
/// </summary>
public sealed class BiblePublicationService(IServiceScopeFactory scopeFactory, ILogger logger) : IBiblePublicationService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    private static readonly TimeSpan PublicationCacheTtl = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<PublicationCacheKey, PublicationCacheEntry> publicationWithSectionsCache = new();
    private readonly ConcurrentDictionary<PublicationCacheKey, PublicationCacheEntry> publicationWithTracksCache = new();

    private readonly record struct PublicationCacheKey(string LanguageCode, string PublicationCode);

    private sealed class PublicationCacheEntry(DateTimeOffset createdAt, Lazy<Task<BiblePublication?>> value)
    {
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public Lazy<Task<BiblePublication?>> Value { get; } = value;
    }

    // Cache for distinct languages (PublicationLanguages is effectively static at runtime).
    // This prevents expensive COUNT/DISTINCT queries during playback navigation (next/prev track),
    // schedule cache refresh, and other state updates.
    private readonly object distinctLanguagesCacheLock = new();
    private Dictionary<string, Language>? cachedDistinctLanguagesAll;
    private readonly Dictionary<string, Dictionary<string, Language>> cachedDistinctLanguagesByCategory =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var key = new PublicationCacheKey(normalizedLanguageCode, publicationCode);
            var now = DateTimeOffset.UtcNow;

            static Lazy<Task<BiblePublication?>> CreateLazy(
                BiblePublicationService self,
                string lang,
                string code,
                CancellationToken ct)
                => new(() => self.LoadPublicationWithSectionsUncachedAsync(lang, code, ct),
                    LazyThreadSafetyMode.ExecutionAndPublication);

            var entry = publicationWithSectionsCache.AddOrUpdate(
                key,
                _ => new PublicationCacheEntry(now, CreateLazy(this, normalizedLanguageCode, publicationCode, cancellationToken)),
                (_, existing) =>
                    now - existing.CreatedAt <= PublicationCacheTtl
                        ? existing
                        : new PublicationCacheEntry(now, CreateLazy(this, normalizedLanguageCode, publicationCode, cancellationToken)));

            try
            {
                return await entry.Value.Value;
            }
            catch
            {
                publicationWithSectionsCache.TryRemove(key, out _);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublication with Sections. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    private async Task<BiblePublication?> LoadPublicationWithSectionsUncachedAsync(
        string normalizedLanguageCode,
        string publicationCode,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        return await dbContext.BiblePublications
            .AsNoTracking()
            .Include(x => x.BiblePublicationCategories)
            .ThenInclude(x => x.Category)
            .Include(x => x.Sections)
                .ThenInclude(s => s.Tracks)
                    .ThenInclude(t => t.TrackUrl)
            .Where(x => x.PublicationCode == publicationCode && x.Language != null && x.Language.LanguageCode == normalizedLanguageCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Use canonical case for drama codes (VODMoviesBibleTimes, etc.); otherwise preserve input
            var lowerCode = publicationCode.ToLowerInvariant();
            var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;

            var key = new PublicationCacheKey(normalizedLanguageCode, publicationCodeForDb);
            var now = DateTimeOffset.UtcNow;

            static Lazy<Task<BiblePublication?>> CreateLazy(
                BiblePublicationService self,
                string lang,
                string code,
                string originalCodeForLog,
                string originalInputCodeForLog,
                CancellationToken ct)
                => new(() => self.LoadPublicationWithTracksUncachedAsync(lang, code, originalCodeForLog, originalInputCodeForLog, ct),
                    LazyThreadSafetyMode.ExecutionAndPublication);

            var entry = publicationWithTracksCache.AddOrUpdate(
                key,
                _ => new PublicationCacheEntry(now, CreateLazy(this, normalizedLanguageCode, publicationCodeForDb, publicationCodeForDb, publicationCode, cancellationToken)),
                (_, existing) =>
                    now - existing.CreatedAt <= PublicationCacheTtl
                        ? existing
                        : new PublicationCacheEntry(now, CreateLazy(this, normalizedLanguageCode, publicationCodeForDb, publicationCodeForDb, publicationCode, cancellationToken)));

            try
            {
                return await entry.Value.Value;
            }
            catch
            {
                publicationWithTracksCache.TryRemove(key, out _);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublication with Tracks. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    private async Task<BiblePublication?> LoadPublicationWithTracksUncachedAsync(
        string normalizedLanguageCode,
        string publicationCodeForDb,
        string publicationCodeForDbForLog,
        string originalPublicationCodeForLog,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Load publication with only non-sectioned tracks (tracks directly under publication, not under a section)
        // Include TrackUrl for CDN URL resolution
        var publication = await dbContext.BiblePublications
            .AsNoTracking()
            .Include(x => x.BiblePublicationCategories)
            .ThenInclude(x => x.Category)
            .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .ThenInclude(t => t.TrackUrl)
            .Where(x => x.PublicationCode == publicationCodeForDb && x.Language != null && x.Language.LanguageCode == normalizedLanguageCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (publication?.Tracks != null && publication.Tracks.Count > 0)
        {
            var distinctByCode = publication.Tracks
                .GroupBy(t => t.TrackCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (distinctByCode.Count < publication.Tracks.Count)
            {
                logger.Debug("GetByLanguageAndCodeWithTracksAsync: Deduplicated tracks by TrackCode for {PublicationCode}: {Original} -> {Deduped}",
                    publicationCodeForDb, publication.Tracks.Count, distinctByCode.Count);
                publication.Tracks = distinctByCode;
            }

            foreach (var track in publication.Tracks)
            {
                track.Publication = publication;
            }
        }

        logger.Debug("GetByLanguageAndCodeWithTracksAsync: Loaded publication={PublicationName}, TracksCount={TracksCount} for language={LanguageCode}, code={PublicationCode} (dbCode={DbCode})",
            publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0, normalizedLanguageCode, originalPublicationCodeForLog, publicationCodeForDbForLog);

        return publication;
    }

    public async Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var query = dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(x => x.Category)
                .Include(x => x.Language)
                .Where(x => x.Language != null && x.Language.LanguageCode == languageCode);

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.BiblePublicationCategories.Any(bpc => bpc.Category.CategoryCode == categoryName));
                if (filterIsMusicWhenMusicCategory && string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsMusic);
                }
            }

            var publicationsList = await query.ToListAsync(cancellationToken);

            logger.Debug("GetByLanguageCodeAsync: Found {PublicationCount} publications for language={LanguageCode}",
                publicationsList.Count, languageCode);

            // Handle potential duplicates gracefully - use first occurrence
            var result = new Dictionary<string, BiblePublication>();
            foreach (var publication in publicationsList)
            {
                logger.Debug("GetByLanguageCodeAsync: Publication code={Code}, name={Name}",
                    publication.PublicationCode, publication.Name);
                if (!result.ContainsKey(publication.PublicationCode))
                {
                    result[publication.PublicationCode] = publication;
                }
                else
                {
                    logger.Warning("Duplicate BiblePublication entry found. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}", languageCode, publication.PublicationCode);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublications by language code. LanguageCode={LanguageCode}", languageCode);
            throw;
        }
    }

    public async Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Fast path: return cached result if available (cache key includes filter for Music so music container vs Bible container don't share)
            var normalizedCategory = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
            var cacheKey = normalizedCategory == null ? null : (filterIsMusicWhenMusicCategory && string.Equals(normalizedCategory, "Music", StringComparison.OrdinalIgnoreCase) ? "Music~IsMusicOnly" : normalizedCategory);
            lock (distinctLanguagesCacheLock)
            {
                if (cacheKey == null && cachedDistinctLanguagesAll != null)
                {
                    // Return a copy to avoid callers mutating the cached dictionary.
                    return new Dictionary<string, Language>(cachedDistinctLanguagesAll);
                }

                if (cacheKey != null &&
                    cachedDistinctLanguagesByCategory.TryGetValue(cacheKey, out var cachedForCategory))
                {
                    return new Dictionary<string, Language>(cachedForCategory);
                }
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Use PublicationLanguage table for discovery - it's designed for this purpose
            // This table tracks which languages are available for each publication code in each category
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => x.Language != null);

            // Filter by category if provided (categoryName is CategoryCode, e.g. "Bible", "Music")
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryCode == categoryName);
                if (filterIsMusicWhenMusicCategory && string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsMusic);
                }
            }

            var publicationLanguagesCount = await query.CountAsync(cancellationToken);
            var distinctLanguages = await query
                .Select(x => x.Language!)
                .Distinct()
                .ToListAsync(cancellationToken);

            logger.Debug("BiblePublicationService.GetDistinctLanguagesAsync: Found {PublicationLanguageCount} PublicationLanguage entries across {LanguageCount} distinct languages",
                publicationLanguagesCount, distinctLanguages.Count);

            var result = distinctLanguages.ToDictionary(x => x.LanguageCode, x => x);

            // Cache result for subsequent calls
            lock (distinctLanguagesCacheLock)
            {
                if (cacheKey == null)
                {
                    cachedDistinctLanguagesAll = result;
                }
                else
                {
                    cachedDistinctLanguagesByCategory[cacheKey] = result;
                }
            }

            return new Dictionary<string, Language>(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting distinct Languages from PublicationLanguages");
            throw;
        }
    }

    public async Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Query PublicationLanguages: current language + non-languaged (LanguageId == null) for the category.
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => (x.Language != null && x.Language.LanguageCode == normalizedLanguageCode) || x.LanguageId == null);

            // Filter by category if provided (categoryName is CategoryCode)
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryCode == categoryName);
                if (filterIsMusicWhenMusicCategory && string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsMusic);
                }
            }

            var publicationCodes = await query
                .Select(x => x.PublicationCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Deduplicate by case-insensitive key while preserving original case from first occurrence
            var uniqueCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in publicationCodes)
            {
                if (!uniqueCodes.ContainsKey(code))
                {
                    uniqueCodes[code] = code;
                }
            }

            var result = uniqueCodes.Values.OrderBy(x => x).ToList();

            logger.Debug("GetAvailablePublicationCodesAsync: Found {Count} available publication codes (deduplicated from {OriginalCount}) for language={LanguageCode}, category={CategoryName}",
                result.Count, publicationCodes.Count, languageCode, categoryName ?? "all");

            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting available publication codes from PublicationLanguages. LanguageCode={LanguageCode}, CategoryName={CategoryName}",
                languageCode, categoryName);
            throw;
        }
    }

    public async Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Query PublicationLanguages: current language + non-languaged (LanguageId == null), ordered by Id.
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => (x.Language != null && x.Language.LanguageCode == normalizedLanguageCode) || x.LanguageId == null);

            // Filter by category if provided (categoryName is CategoryCode)
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryCode == categoryName);
                if (filterIsMusicWhenMusicCategory && string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsMusic);
                }
            }

            var firstPublicationCode = await query
                .OrderBy(x => x.Id)
                .Select(x => x.PublicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            logger.Debug("GetFirstPublicationCodeByOrderAsync: Found first publication code={PublicationCode} for language={LanguageCode}, category={CategoryName}",
                firstPublicationCode ?? "(null)", languageCode, categoryName ?? "all");

            return firstPublicationCode;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting first publication code by order from PublicationLanguages. LanguageCode={LanguageCode}, CategoryName={CategoryName}",
                languageCode, categoryName);
            throw;
        }
    }

    public async Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var exists = await dbContext.BiblePublications
                .AsNoTracking()
                .AnyAsync(p => p.PublicationCode == publicationCode && p.LanguageId == null, cancellationToken);

            logger.Debug("IsNoLanguagePublicationAsync: Publication {PublicationCode} has no language: {IsNoLanguage}",
                publicationCode, exists);

            return exists;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking if publication has no language. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var pub = await GetByLanguageAndCodeWithSectionsAsync(languageCode, publicationCode, cancellationToken);
            if (pub != null)
            {
                var categoryCode = pub.PrimaryCategory?.CategoryCode;
                return (categoryCode, pub.IsMusic);
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var noLangPub = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.BiblePublicationCategories)
                .ThenInclude(x => x.Category)
                .Where(x => x.PublicationCode == publicationCode && x.LanguageId == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (noLangPub != null)
            {
                var catCode = noLangPub.PrimaryCategory?.CategoryCode;
                return (catCode, noLangPub.IsMusic);
            }

            var pl = await dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.PublicationCode == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (pl?.Category == null)
            {
                return null;
            }

            var categoryCodeFromDiscovery = pl.Category.CategoryCode;
            var isMusicInferred = !JwSourceHelper.IsMusicExcludedPublicationCodes.Contains(publicationCode) &&
                (string.Equals(categoryCodeFromDiscovery, "Music", StringComparison.OrdinalIgnoreCase) ||
                 JwSourceHelper.IsMusicPublicationCode(publicationCode));
            return (categoryCodeFromDiscovery, isMusicInferred);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting publication category info. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var discoveredForLanguage = await GetAvailablePublicationCodesAsync(languageCode, categoryCode, false, cancellationToken);
            var discoveredForDefault = await GetAvailablePublicationCodesAsync(AppConstants.Media.DefaultLanguageCode, categoryCode, false, cancellationToken);
            var harvested = await GetByLanguageCodeAsync(languageCode, categoryCode, false, cancellationToken);

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in discoveredForLanguage)
            {
                codes.Add(code);
            }
            foreach (var code in discoveredForDefault)
            {
                codes.Add(code);
            }
            foreach (var code in harvested.Keys)
            {
                codes.Add(code);
            }

            var comparer = PublicationCodeHelper.GetPublicationCodeComparerForCategory(categoryCode);
            return codes.OrderBy(c => c, comparer).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting publication codes in category order. LanguageCode={LanguageCode}, CategoryCode={CategoryCode}",
                languageCode, categoryCode);
            throw;
        }
    }

    public void InvalidatePublicationCaches(string languageCode, string publicationCode)
    {
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var lowerCode = publicationCode.ToLowerInvariant();
        var publicationCodeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;
        var key = new PublicationCacheKey(normalizedLanguageCode, publicationCodeForDb);
        publicationWithTracksCache.TryRemove(key, out _);
        publicationWithSectionsCache.TryRemove(key, out _);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
    }
}

