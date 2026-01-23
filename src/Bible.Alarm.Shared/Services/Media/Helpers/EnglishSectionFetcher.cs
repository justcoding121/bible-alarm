#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for fetching English publication sections.
/// </summary>
internal sealed class EnglishSectionFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly EnglishTrackParser trackParser;

    public EnglishSectionFetcher(HttpClient httpClient, ILogger logger, EnglishTrackParser trackParser)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.trackParser = trackParser ?? throw new ArgumentNullException(nameof(trackParser));
    }

    public async Task<(List<BiblePublicationSection> Sections, string? LocalizedPubName)> FetchSectionsAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string categoryName,
        List<string> sectionCodes,
        CancellationToken cancellationToken)
    {
        // Get BaseUrl
        var baseUrl = await db.BaseUrls
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .FirstOrDefaultAsync(cancellationToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return (new List<BiblePublicationSection>(), null);
        }

        // Determine if this is a Bible publication (uses booknum) or iam (Kingdom Melodies)
        var isBible = categoryName.Equals("Bible", StringComparison.OrdinalIgnoreCase);
        var isIamPublication = normalizedPublicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase);
        var sections = new List<BiblePublicationSection>();
        string? localizedPubName = null;

        foreach (var sectionCode in sectionCodes)
        {
            try
            {
                var section = await FetchSingleSectionAsync(
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode,
                    isBible, isIamPublication, baseUrl, cancellationToken);

                if (section == null)
                {
                    continue;
                }

                // Extract localized publication name (only once, from first successful section)
                if (localizedPubName == null)
                {
                    localizedPubName = await ExtractPublicationNameAsync(
                        sectionCode, normalizedPublicationCode, normalizedLanguageCode,
                        isBible, isIamPublication, cancellationToken);
                }

                sections.Add(section);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                logger.Debug("Section {SectionCode} not available for publication {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch section {SectionCode} for publication {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
                continue;
            }
        }

        return (sections, localizedPubName);
    }

    private async Task<BiblePublicationSection?> FetchSingleSectionAsync(
        string sectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        bool isBible,
        bool isIamPublication,
        BaseUrl baseUrl,
        CancellationToken cancellationToken)
    {
        // For Bible, use booknum parameter; for iam (Kingdom Melodies), use pub=sectionCode with langwritten=E
        // Note: iam uses langwritten=E even though it has no language (melody music)
        var harvestLink = isBible
            ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
            : isIamPublication
                ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten=E"
                : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

        var response = await httpClient.GetAsync(harvestLink, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            return null;
        }

        // Extract section name
        string? sectionName = null;
        if (root.TryGetProperty("pubName", out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
        }

        // Create section
        var section = new BiblePublicationSection
        {
            Name = sectionName ?? sectionCode,
            SectionCode = sectionCode.ToLowerInvariant(),
            UrlParams = new List<UrlParam>(),
            Tracks = new List<BiblePublicationTrack>()
        };

        // Parse tracks based on publication type
        if (isIamPublication)
        {
            var tracks = trackParser.ParseIamTracks(filesElement, sectionCode, baseUrl);
            section.Tracks.AddRange(tracks);
        }
        else if (isBible)
        {
            var tracks = trackParser.ParseBibleTracks(
                filesElement, normalizedLanguageCode, normalizedPublicationCode, sectionCode, baseUrl);
            section.Tracks.AddRange(tracks);
        }

        // Add URL params based on type
        if (isBible)
        {
            section.UrlParams.Add(new UrlParam
            {
                Key = "pub",
                Value = normalizedPublicationCode,
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            });
            section.UrlParams.Add(new UrlParam
            {
                Key = "booknum",
                Value = sectionCode,
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            });
        }
        else
        {
            section.UrlParams.Add(new UrlParam
            {
                Key = "pub",
                Value = sectionCode,
                IsQueryParam = true,
                BaseUrl = baseUrl,
                BaseUrlId = baseUrl.Id
            });
        }

        section.UrlParams.Add(new UrlParam
        {
            Key = "fileformat",
            Value = "mp3",
            IsQueryParam = true,
            BaseUrl = baseUrl,
            BaseUrlId = baseUrl.Id
        });

        return section;
    }

    private async Task<string?> ExtractPublicationNameAsync(
        string sectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        bool isBible,
        bool isIamPublication,
        CancellationToken cancellationToken)
    {
        // For iam, always use "Kingdom Melodies" as publication name
        if (isIamPublication)
        {
            return "Kingdom Melodies";
        }

        // For Bible, fetch one section to get the publication name
        var harvestLink = isBible
            ? $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}"
            : $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";

        try
        {
            var response = await httpClient.GetAsync(harvestLink, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Extract localized publication name from parentPubName
            if (root.TryGetProperty("parentPubName", out var parentPubNameElement))
            {
                var rawName = parentPubNameElement.GetString();
                var extractedName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                
                // Validate that the extracted name is not a known video/drama publication name
                if (!string.IsNullOrEmpty(extractedName) && isBible)
                {
                    var knownVideoNames = new[] { "The Good News According to Jesus", "Good news according to Jesus" };
                    var isVideoName = knownVideoNames.Any(vn => extractedName.Contains(vn, StringComparison.OrdinalIgnoreCase));
                    
                    if (isVideoName)
                    {
                        logger.Warning("API returned video/drama publication name '{ExtractedName}' for Bible publication {PublicationCode} in language {LanguageCode}. This is likely an API error. Skipping this name and using fallback.",
                            extractedName, normalizedPublicationCode, normalizedLanguageCode);
                        extractedName = null;
                    }
                }
                
                return extractedName;
            }

            // For Drama, also check category.name
            if (!isBible && root.TryGetProperty("category", out var categoryElement) &&
                categoryElement.TryGetProperty("name", out var categoryNameElement))
            {
                var rawName = categoryNameElement.GetString();
                return rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to extract publication name for {PublicationCode}", normalizedPublicationCode);
        }

        return null;
    }
}
