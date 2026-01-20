#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;

using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for constructing download URLs from the database using joins.
/// </summary>
public class UrlConstructionService : IUrlConstructionService
{
    private readonly MediaDbContext _dbContext;

    public UrlConstructionService(MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Constructs download URLs for a Bible publication track.
    /// Returns both primary and backup URLs (one for each BaseUrl in the database).
    /// </summary>
    /// <param name="trackId">The ID of the BiblePublicationTrack</param>
    /// <returns>List of constructed URLs (primary and backup)</returns>
    public async Task<List<string>> ConstructTrackUrlsAsync(int trackId)
    {
        // Single query with all necessary joins to minimize database round trips
        var track = await _dbContext.BiblePublicationTracks
            .AsNoTracking() // Read-only, improves performance
            .Include(t => t.Publication)
                .ThenInclude(p => p!.UrlParams)
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
                .ThenInclude(s => s!.UrlParams)
            .Include(t => t.UrlParams)
            .FirstOrDefaultAsync(t => t.Id == trackId);

        if (track == null || track.Publication == null)
        {
            return new List<string>();
        }

        // Get all base URLs from the database (all downloads use the same ApiUrls)
        var baseUrls = await _dbContext.BaseUrls
            .AsNoTracking()
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .Include(bu => bu.UrlParams)
            .ToListAsync();

        if (baseUrls == null || baseUrls.Count == 0)
        {
            return new List<string>(); // Or throw exception if this should never happen
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

        // Add parameters from BiblePublication
        foreach (var param in publication.UrlParams)
        {
            allParams[param.Key] = param.Value;
            if (param.IsQueryParam)
            {
                queryParams[param.Key] = param.Value;
            }
        }

        // Add parameters from BiblePublicationSection if track has a section
        if (track.Section != null)
        {
            foreach (var param in track.Section.UrlParams)
            {
                allParams[param.Key] = param.Value;
                if (param.IsQueryParam)
                {
                    queryParams[param.Key] = param.Value;
                }
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
        var query = _dbContext.BiblePublicationTracks
            .AsNoTracking() // Read-only, improves performance
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Publication)
                .ThenInclude(p => p!.UrlParams)
            .Include(t => t.Section)
                .ThenInclude(s => s!.UrlParams)
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

        return await ConstructTrackUrlsAsync(track.Id);
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
        var query = _dbContext.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.Publication)
                .ThenInclude(p => p!.Language)
            .Include(t => t.Publication)
                .ThenInclude(p => p!.UrlParams)
            .Include(t => t.Section)
                .ThenInclude(s => s!.UrlParams)
            .Include(t => t.UrlParams)
            .Where(t => t.Publication != null
                && t.Publication.Code == publicationCode
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

        // Get the first BaseUrl from the database (all downloads use the same ApiUrls)
        var baseUrl = await _dbContext.BaseUrls
            .AsNoTracking()
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .Include(bu => bu.UrlParams)
            .FirstOrDefaultAsync();

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
