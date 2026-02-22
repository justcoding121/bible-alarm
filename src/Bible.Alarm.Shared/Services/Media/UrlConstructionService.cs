#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for constructing download URLs from track UrlParams and fixed base URLs.
/// </summary>
public class UrlConstructionService : IUrlConstructionService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly TimeSpan LookUpPathCacheTtl = TimeSpan.FromMinutes(5);

    private readonly record struct LookUpPathCacheKey(string PublicationCode, string LanguageCode, string SectionCode, string TrackCode);

    private sealed class LookUpPathCacheEntry(DateTimeOffset createdAt, Lazy<Task<string?>> value)
    {
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public Lazy<Task<string?>> Value { get; } = value;
    }

    private readonly ConcurrentDictionary<LookUpPathCacheKey, LookUpPathCacheEntry> lookUpPathCache = new();

    public UrlConstructionService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Constructs download URLs for a Bible publication track.
    /// Returns both primary and backup URLs (one per base URL constant).
    /// </summary>
    /// <param name="trackId">The ID of the BiblePublicationTrack</param>
    /// <returns>List of constructed URLs (primary and backup)</returns>
    public async Task<List<string>> ConstructTrackUrlsAsync(int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var track = await dbContext.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Include(t => t.UrlParams)
            .FirstOrDefaultAsync(t => t.Id == trackId);

        if (track == null || track.Publication == null)
        {
            return new List<string>();
        }

        var baseUrls = AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls;
        var urls = new List<string>();
        foreach (var baseUrl in baseUrls)
        {
            var url = ConstructUrl(baseUrl, track);
            if (!string.IsNullOrEmpty(url))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    /// <summary>
    /// Constructs a single URL for a track using a base URL string.
    /// </summary>
    private static string ConstructUrl(string baseUrl, BiblePublicationTrack track)
    {
        var queryParams = new Dictionary<string, string>();
        foreach (var param in track.UrlParams.Where(p => p.IsQueryParam))
        {
            queryParams[param.Key] = param.Value;
        }

        if (!queryParams.ContainsKey("output"))
        {
            queryParams["output"] = "json";
        }

        if (queryParams.TryGetValue("fileformat", out var ff))
        {
            queryParams["fileformat"] = ff.ToUpperInvariant();
        }

        if (queryParams.Count == 0)
        {
            return baseUrl.TrimEnd('/');
        }

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{baseUrl.TrimEnd('/')}?{queryString}";
    }

    /// <summary>
    /// Constructs download URLs for a track by publication code, language code, section code, and track number.
    /// </summary>
    public async Task<List<string>> ConstructTrackUrlsAsync(
        string publicationCode,
        string languageCode,
        string? sectionCode,
        string trackCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var isNoLanguagePublication = await dbContext.BiblePublications
            .AsNoTracking()
            .AnyAsync(p => p.PublicationCode == publicationCode && p.LanguageId == null);

        var normalizedTrackCode = trackCode ?? string.Empty;

        var query = dbContext.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Include(t => t.UrlParams)
            .Where(t => t.Publication != null && t.Publication.PublicationCode == publicationCode);

        if (isNoLanguagePublication)
        {
            query = query.Where(t => t.Publication!.Language == null);
        }
        else
        {
            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            query = query.Where(t => t.Publication!.Language != null
                && t.Publication.Language.LanguageCode == normalizedLanguageCode);
        }

        if (!string.IsNullOrEmpty(sectionCode))
        {
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            query = query.Where(t => t.Section != null && t.Section.SectionCode == normalizedSectionCode);
        }
        else
        {
            query = query.Where(t => t.Section == null);
        }

        // Query by TrackCode (string)
        query = query.Where(t => t.TrackCode == normalizedTrackCode);

        var track = await query.SingleOrDefaultAsync();

        if (track == null)
        {
            return new List<string>();
        }

        var baseUrls = AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls;
        var urls = new List<string>();
        foreach (var baseUrl in baseUrls)
        {
            var url = ConstructUrl(baseUrl, track);
            if (!string.IsNullOrEmpty(url))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    /// <summary>
    /// Constructs the lookup path (query string) for a track by publication code, language code, section code, and track number.
    /// Returns the query string part (e.g., "?output=json&pub=nwt&booknum=1&fileformat=MP3&langwritten=E&track=1").
    /// </summary>
    public async Task<string?> ConstructTrackLookUpPathAsync(
        string publicationCode,
        string? languageCode,
        string? sectionCode,
        string trackCode)
    {
        var normalizedPublicationCode = publicationCode ?? string.Empty;
        var normalizedLanguageCode = languageCode ?? string.Empty;
        var normalizedSectionCode = sectionCode ?? string.Empty;
        var key = new LookUpPathCacheKey(normalizedPublicationCode, normalizedLanguageCode, normalizedSectionCode, trackCode);
        var now = DateTimeOffset.UtcNow;

        static Lazy<Task<string?>> CreateLazy(
            UrlConstructionService self,
            string pub,
            string? lang,
            string? section,
            string track)
            => new(() => self.LoadTrackLookUpPathUncachedAsync(pub, lang, section, track),
                LazyThreadSafetyMode.ExecutionAndPublication);

        var entry = lookUpPathCache.AddOrUpdate(
            key,
            _ => new LookUpPathCacheEntry(now, CreateLazy(this, normalizedPublicationCode, languageCode, sectionCode, trackCode ?? string.Empty)),
            (_, existing) =>
                now - existing.CreatedAt <= LookUpPathCacheTtl
                    ? existing
                    : new LookUpPathCacheEntry(now, CreateLazy(this, normalizedPublicationCode, languageCode, sectionCode, trackCode ?? string.Empty)));

        try
        {
            return await entry.Value.Value;
        }
        catch
        {
            // If the cached task fails, remove it so next call can retry.
            lookUpPathCache.TryRemove(key, out _);
            throw;
        }
    }

    private async Task<string?> LoadTrackLookUpPathUncachedAsync(
        string publicationCode,
        string? languageCode,
        string? sectionCode,
        string trackCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var normalizedTrackCode = trackCode ?? string.Empty;

        // Check if this publication exists without a language (e.g., instrumental music)
        var isNoLanguagePublication = await dbContext.BiblePublications
            .AsNoTracking()
            .AnyAsync(p => p.PublicationCode == publicationCode && p.LanguageId == null);

        var effectiveLanguageCode = isNoLanguagePublication ? null : languageCode;

        var query = dbContext.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Include(t => t.UrlParams)
            .Where(t => t.Publication != null && t.Publication.PublicationCode == publicationCode);

        if (!string.IsNullOrEmpty(effectiveLanguageCode))
        {
            var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();
            query = query.Where(t => t.Publication!.Language != null && t.Publication.Language.LanguageCode == normalizedLanguageCode);
        }
        else
        {
            query = query.Where(t => t.Publication!.Language == null);
        }

        if (!string.IsNullOrEmpty(sectionCode))
        {
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            query = query.Where(t => t.Section != null && t.Section.SectionCode == normalizedSectionCode);
        }
        else
        {
            query = query.Where(t => t.Section == null);
        }

        // Resolve track by TrackCode (string)
        query = query.Where(t => t.TrackCode == normalizedTrackCode);

        var track = await query.SingleOrDefaultAsync();

        if (track == null || track.Publication == null)
        {
            return null;
        }

        var baseUrl = AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrls[0];
        var fullUrl = ConstructUrl(baseUrl, track);

        if (string.IsNullOrEmpty(fullUrl))
        {
            return null;
        }

        // Extract query string from full URL
        var uri = new Uri(fullUrl);
        var queryString = uri.Query;
        
        // Return with leading ? if not empty
        return string.IsNullOrEmpty(queryString) ? null : queryString;
    }
}
