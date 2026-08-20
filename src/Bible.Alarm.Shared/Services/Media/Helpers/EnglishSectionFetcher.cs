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
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code", StringComparison.Ordinal))
            {
                logger.Debug(ex, "Section {SectionCode} not available for publication {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to fetch section {SectionCode} for publication {PublicationCode} in English",
                    sectionCode, normalizedPublicationCode);
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

        var queryString = BuildEnglishSectionQueryString(
            sectionCode,
            normalizedPublicationCode,
            normalizedLanguageCode,
            isIssueSectioned,
            isBible,
            publicationWithoutLanguage,
            fileFormat);

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

        var sectionName = ResolveEnglishSectionName(root, isIssueSectioned);

        var section = new BiblePublicationSection
        {
            Name = sectionName ?? sectionCode,
            SectionCode = sectionCode.ToLowerInvariant(),
            Tracks = new List<BiblePublicationTrack>()
        };

        PopulateEnglishSectionTracks(
            section,
            filesElement,
            isIssueSectioned,
            publicationWithoutLanguage,
            isBible,
            normalizedLanguageCode,
            fileFormat);

        return section;
    }

    private static string BuildEnglishSectionQueryString(
        string sectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        bool isIssueSectioned,
        bool isBible,
        bool publicationWithoutLanguage,
        string fileFormat)
    {
        if (isIssueSectioned)
        {
            var (apiPubCode, issueCode) = MagazineHelper.ParseSectionCode(sectionCode);
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={apiPubCode}&{AppConstants.Media.GetPubQueryParamName.Issue}={issueCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

        if (isBible)
        {
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={normalizedPublicationCode}&{AppConstants.Media.GetPubQueryParamName.BookNum}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

        if (publicationWithoutLanguage)
        {
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}";
        }

        return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
    }

    private static string? ResolveEnglishSectionName(JsonElement root, bool isIssueSectioned)
    {
        if (isIssueSectioned)
        {
            string? pubName = null;
            string? formattedDate = null;
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pnElement))
                pubName = pnElement.GetString();
            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.FormattedDate, out var fdElement))
                formattedDate = fdElement.GetString();
            return MagazineHelper.BuildSectionName(pubName, formattedDate);
        }

        if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            return MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
        }

        return null;
    }

    private static void PopulateEnglishSectionTracks(
        BiblePublicationSection section,
        JsonElement filesElement,
        bool isIssueSectioned,
        bool publicationWithoutLanguage,
        bool isBible,
        string normalizedLanguageCode,
        string fileFormat)
    {
        IEnumerable<BiblePublicationTrack> tracks;
        if (isIssueSectioned)
        {
            tracks = EnglishTrackParser.ParseGenericTracks(
                filesElement, normalizedLanguageCode, AppConstants.Media.MediaStreamFormatMp3);
        }
        else if (publicationWithoutLanguage)
        {
            tracks = EnglishTrackParser.ParseIamTracks(filesElement);
        }
        else if (isBible)
        {
            tracks = EnglishTrackParser.ParseBibleTracks(filesElement, normalizedLanguageCode);
        }
        else
        {
            tracks = EnglishTrackParser.ParseGenericTracks(
                filesElement, normalizedLanguageCode, fileFormat);
        }

        section.Tracks.AddRange(tracks);
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

        string queryString;
        if (isBible)
        {
            queryString = $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={normalizedPublicationCode}&{AppConstants.Media.GetPubQueryParamName.BookNum}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }
        else
        {
            queryString = $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        }

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
