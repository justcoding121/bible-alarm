#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for constructing download URLs from the database using joins.
/// </summary>
public class UrlConstructionService : IUrlConstructionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object baseUrlsLock = new();
    private Task<List<BaseUrl>>? baseUrlsTask;

    private static readonly TimeSpan LookUpPathCacheTtl = TimeSpan.FromMinutes(5);

    private readonly record struct LookUpPathCacheKey(string PublicationCode, string LanguageCode, string SectionCode, int TrackNumber);

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

    private Task<List<BaseUrl>> GetBaseUrlsAsync()
    {
        lock (baseUrlsLock)
        {
            baseUrlsTask ??= LoadBaseUrlsAsync();
            return baseUrlsTask;
        }
    }

    private async Task<List<BaseUrl>> LoadBaseUrlsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        return await dbContext.BaseUrls
            .AsNoTracking()
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .Include(bu => bu.UrlParams)
            .ToListAsync();
    }

    /// <summary>
    /// Constructs download URLs for a Bible publication track.
    /// Returns both primary and backup URLs (one for each BaseUrl in the database).
    /// </summary>
    /// <param name="trackId">The ID of the BiblePublicationTrack</param>
    /// <returns>List of constructed URLs (primary and backup)</returns>
    public async Task<List<string>> ConstructTrackUrlsAsync(int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Single query with all necessary joins to minimize database round trips
        var track = await dbContext.BiblePublicationTracks
            .AsNoTracking() // Read-only, improves performance
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Include(t => t.UrlParams)
            .FirstOrDefaultAsync(t => t.Id == trackId);

        if (track == null || track.Publication == null)
        {
            return new List<string>();
        }

        // BaseUrls are static at runtime; cache them to avoid repeated DB queries.
        var baseUrls = await GetBaseUrlsAsync();

        if (baseUrls == null || baseUrls.Count == 0)
        {
            // Or throw exception if this should never happen
            return new List<string>();
        }

        var urls = new List<string>();

        // Get all base URLs from the database
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
    /// Constructs a single URL for a track using a specific BaseUrl.
    /// </summary>
    private string ConstructUrl(BaseUrl baseUrl, BiblePublicationTrack track)
    {
        var publication = track.Publication;
        if (publication == null)
        {
            return string.Empty;
        }

        // Build base URL: BaseUrl + "/" + PathPrefix
        var baseUrlString = baseUrl.Url.TrimEnd('/');
        var pathPrefix = baseUrl.PathPrefix.TrimStart('/');
        var fullBaseUrl = $"{baseUrlString}/{pathPrefix}";

        // Collect all parameters (both query params and path params)
        var allParams = new Dictionary<string, string>();
        var queryParams = new Dictionary<string, string>();

        // Add parameters from BaseUrl
        foreach (var param in baseUrl.UrlParams)
        {
            allParams[param.Key] = param.Value;
            if (param.IsQueryParam)
            {
                queryParams[param.Key] = param.Value;
            }
        }

        // Add parameters from BiblePublicationTrack
        foreach (var param in track.UrlParams)
        {
            allParams[param.Key] = param.Value;
            if (param.IsQueryParam)
            {
                queryParams[param.Key] = param.Value;
            }
        }

        // Replace {key} placeholders in the URL path with values from non-query params
        var urlPath = fullBaseUrl;
        foreach (var param in allParams)
        {
            if (!queryParams.ContainsKey(param.Key)) // Only replace non-query params in the path
            {
                var placeholder = $"{{{param.Key}}}";
                if (urlPath.Contains(placeholder))
                {
                    urlPath = urlPath.Replace(placeholder, Uri.EscapeDataString(param.Value));
                }
            }
        }

        // Convert fileformat to uppercase (API expects MP3/MP4, not mp3/mp4)
        if (queryParams.ContainsKey("fileformat"))
        {
            queryParams["fileformat"] = queryParams["fileformat"].ToUpperInvariant();
        }

        // Build query string in the expected order: output, pub, booknum, fileformat, alllangs, langwritten, track
        var orderedKeys = new[] { "output", "pub", "booknum", "fileformat", "alllangs", "langwritten", "track" };
        var queryParts = new List<string>();
        
        // Add parameters in the specified order
        foreach (var key in orderedKeys)
        {
            if (queryParams.ContainsKey(key))
            {
                queryParts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(queryParams[key])}");
            }
        }
        
        // Add any remaining query parameters that weren't in the ordered list
        foreach (var kvp in queryParams.Where(kvp => !orderedKeys.Contains(kvp.Key)))
        {
            queryParts.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
        }

        // Build final URL
        if (queryParts.Count > 0)
        {
            var queryString = string.Join("&", queryParts);
            return $"{urlPath}?{queryString}";
        }
        
        return urlPath;
    }

    /// <summary>
    /// Constructs download URLs for a track by publication code, language code, section code, and track number.
    /// </summary>
    public async Task<List<string>> ConstructTrackUrlsAsync(
        string publicationCode,
        string languageCode,
        string? sectionCode,
        int trackNumber)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var baseUrls = await GetBaseUrlsAsync();
        if (baseUrls.Count == 0)
        {
            return new List<string>();
        }

        var query = dbContext.BiblePublicationTracks
            .AsNoTracking() // Read-only, improves performance
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Include(t => t.UrlParams)
            .Where(t => t.Publication != null
                && t.Publication.PublicationCode == publicationCode
                && t.Publication.Language != null
                && t.Publication.Language.LanguageCode == languageCode
                && t.Number == trackNumber);

        // Filter by section if provided
        if (!string.IsNullOrEmpty(sectionCode))
        {
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            query = query.Where(t => t.Section != null && t.Section.SectionCode == normalizedSectionCode);
        }
        else
        {
            query = query.Where(t => t.Section == null);
        }

        var track = await query.FirstOrDefaultAsync();

        if (track == null)
        {
            return new List<string>();
        }

        // Avoid re-querying the DB: we already loaded the track (+ params) above.
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
    /// Uses the first BaseUrl from the database.
    /// </summary>
    public async Task<string?> ConstructTrackLookUpPathAsync(
        string publicationCode,
        string? languageCode,
        string? sectionCode,
        int trackNumber)
    {
        var normalizedPublicationCode = publicationCode ?? string.Empty;
        var normalizedLanguageCode = languageCode ?? string.Empty;
        var normalizedSectionCode = sectionCode ?? string.Empty;
        var key = new LookUpPathCacheKey(normalizedPublicationCode, normalizedLanguageCode, normalizedSectionCode, trackNumber);
        var now = DateTimeOffset.UtcNow;

        static Lazy<Task<string?>> CreateLazy(
            UrlConstructionService self,
            string pub,
            string? lang,
            string? section,
            int track)
            => new(() => self.LoadTrackLookUpPathUncachedAsync(pub, lang, section, track),
                LazyThreadSafetyMode.ExecutionAndPublication);

        var entry = lookUpPathCache.AddOrUpdate(
            key,
            _ => new LookUpPathCacheEntry(now, CreateLazy(this, normalizedPublicationCode, languageCode, sectionCode, trackNumber)),
            (_, existing) =>
                now - existing.CreatedAt <= LookUpPathCacheTtl
                    ? existing
                    : new LookUpPathCacheEntry(now, CreateLazy(this, normalizedPublicationCode, languageCode, sectionCode, trackNumber)));

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
        int trackNumber)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var query = dbContext.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Include(t => t.UrlParams)
            .Where(t => t.Publication != null
                && t.Publication.PublicationCode == publicationCode
                && t.Number == trackNumber);

        // Filter by language if provided
        if (!string.IsNullOrEmpty(languageCode))
        {
            query = query.Where(t => t.Publication!.Language != null && t.Publication.Language.LanguageCode == languageCode);
        }
        else
        {
            // For publications without language (e.g., melodies), allow null language
            query = query.Where(t => t.Publication!.Language == null);
        }

        // Filter by section if provided
        if (!string.IsNullOrEmpty(sectionCode))
        {
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            query = query.Where(t => t.Section != null && t.Section.SectionCode == normalizedSectionCode);
        }
        else
        {
            query = query.Where(t => t.Section == null);
        }

        var track = await query.FirstOrDefaultAsync();

        if (track == null || track.Publication == null)
        {
            return null;
        }

        // BaseUrls are static at runtime; cache them to avoid repeated DB queries.
        var baseUrl = (await GetBaseUrlsAsync()).FirstOrDefault();

        if (baseUrl == null)
        {
            return null;
        }

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
