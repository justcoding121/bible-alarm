#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
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

    public EnglishSectionFetcher(HttpClient httpClient, ILogger logger)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(List<BiblePublicationSection> Sections, string? LocalizedPubName)> FetchSectionsAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string categoryName,
        List<string> sectionCodes,
        CancellationToken cancellationToken)
    {
        var isVideo = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
        return await FetchSectionsAsync(new FetchEnglishSectionsRequest(
            db, normalizedPublicationCode, normalizedLanguageCode, categoryName, sectionCodes, isVideo, cancellationToken));
    }

    public async Task<(List<BiblePublicationSection> Sections, string? LocalizedPubName)> FetchSectionsAsync(
        FetchEnglishSectionsRequest request)
    {
        var db = request.Db;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var normalizedLanguageCode = request.NormalizedLanguageCode;
        var categoryName = request.CategoryName;
        var sectionCodes = request.SectionCodes;
        var isVideo = request.IsVideo;
        var cancellationToken = request.CancellationToken;

        // Data-driven: Check if publication has LanguageId == null (determines API parameter pattern)
        var isBible = categoryName.Equals(AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase);
        var fileFormat = isVideo ? AppConstants.Media.MediaStreamFormatMp4 : AppConstants.Media.MediaStreamFormatMp3;
        var publicationWithoutLanguage = await db.BiblePublications
            .AsNoTracking()
            .AnyAsync(bp => bp.PublicationCode == normalizedPublicationCode && bp.LanguageId == null, cancellationToken);
        
        // Also check PublicationLanguages for entries with LanguageId == null
        if (!publicationWithoutLanguage)
        {
            publicationWithoutLanguage = await db.PublicationLanguages
                .AsNoTracking()
                .AnyAsync(pl => pl.PublicationCode == normalizedPublicationCode && pl.LanguageId == null, cancellationToken);
        }
        
        var sections = new List<BiblePublicationSection>();
        string? localizedPubName = null;

        foreach (var sectionCode in sectionCodes)
        {
            try
            {
                var section = await FetchSingleSectionAsync(
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode,
                    isBible, publicationWithoutLanguage, fileFormat, cancellationToken);

                if (section == null)
                {
                    continue;
                }

                // Extract localized publication name (only once, from first successful section)
                if (localizedPubName == null)
                {
                    localizedPubName = await ExtractPublicationNameAsync(
                        sectionCode, normalizedPublicationCode, normalizedLanguageCode,
                        isBible, publicationWithoutLanguage, fileFormat, cancellationToken);
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
        bool publicationWithoutLanguage,
        string fileFormat,
        CancellationToken cancellationToken)
    {
        var isIssueSectioned = MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode);

        string queryString;
        if (isIssueSectioned)
        {
            var (apiPubCode, issueCode) = MagazineHelper.ParseSectionCode(sectionCode);
            queryString = $"?output=json&pub={apiPubCode}&issue={issueCode}&fileformat={AppConstants.Media.MediaStreamFormatMp3}&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else if (isBible)
        {
            queryString = $"?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else if (publicationWithoutLanguage)
        {
            queryString = $"?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={AppConstants.Media.DefaultLanguageCode}";
        }
        else
        {
            queryString = $"?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
        if (jsonString == null)
        {
            return null;
        }
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
        {
            return null;
        }

        string? sectionName = null;
        if (isIssueSectioned)
        {
            string? pubName = null;
            string? formattedDate = null;
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pnElement))
                pubName = pnElement.GetString();
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.FormattedDate, out var fdElement))
                formattedDate = fdElement.GetString();
            sectionName = MagazineHelper.BuildSectionName(pubName, formattedDate);
        }
        else if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            sectionName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
        }

        var section = new BiblePublicationSection
        {
            Name = sectionName ?? sectionCode,
            SectionCode = sectionCode.ToLowerInvariant(),
            Tracks = new List<BiblePublicationTrack>()
        };

        if (isIssueSectioned)
        {
            var tracks = EnglishTrackParser.ParseGenericTracks(
                filesElement, normalizedLanguageCode, AppConstants.Media.MediaStreamFormatMp3);
            section.Tracks.AddRange(tracks);
        }
        else if (publicationWithoutLanguage)
        {
            var tracks = EnglishTrackParser.ParseIamTracks(filesElement);
            section.Tracks.AddRange(tracks);
        }
        else if (isBible)
        {
            var tracks = EnglishTrackParser.ParseBibleTracks(
                filesElement, normalizedLanguageCode);
            section.Tracks.AddRange(tracks);
        }
        else
        {
            var tracks = EnglishTrackParser.ParseGenericTracks(
                filesElement, normalizedLanguageCode, fileFormat);
            section.Tracks.AddRange(tracks);
        }

        return section;
    }

    private async Task<string?> ExtractPublicationNameAsync(
        string sectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        bool isBible,
        bool publicationWithoutLanguage,
        string fileFormat,
        CancellationToken cancellationToken)
    {
        if (MagazineHelper.IsMagazinePublicationCode(normalizedPublicationCode))
        {
            var year = MagazineHelper.GetYear(normalizedPublicationCode);
            return year.ToString();
        }

        // For publications without language (like instrumental music), try to get name from database
        // If not found, use a generic name based on category
        if (publicationWithoutLanguage)
        {
            // Try to get publication name from database if it exists
            // Otherwise, we'll extract it from the API response below
            // For now, continue to API extraction for consistency
        }

        var queryString = isBible
            ? $"?output=json&pub={normalizedPublicationCode}&booknum={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}"
            : $"?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";

        try
        {
            var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
            var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
            if (jsonString == null)
            {
                return null;
            }
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Extract localized publication name from parentPubName
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.ParentPubName, out var parentPubNameElement))
            {
                var rawName = parentPubNameElement.GetString();
                var extractedName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
                
                // Validate that the extracted name is not a known video/drama publication name
                if (!string.IsNullOrEmpty(extractedName) && isBible)
                {
                    var isVideoName = AppConstants.Media.ApiMisleadingGoodNewsVideoPublicationNamePhrases.Any(vn =>
                        extractedName.Contains(vn, StringComparison.OrdinalIgnoreCase));
                    
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
            if (!isBible && root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var categoryElement) &&
                categoryElement.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var categoryNameElement))
            {
                var rawName = categoryNameElement.GetString();
                return MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to extract publication name for {PublicationCode}", normalizedPublicationCode);
        }

        return null;
    }
}
