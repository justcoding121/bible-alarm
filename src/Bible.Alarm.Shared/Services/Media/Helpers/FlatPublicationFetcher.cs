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
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for fetching flat-track publications (Music and Video).
/// </summary>
internal sealed class FlatPublicationFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly VideoLocalizedNameFetcher videoLocalizedNameFetcher;

    public FlatPublicationFetcher(HttpClient httpClient, ILogger logger, VideoLocalizedNameFetcher videoLocalizedNameFetcher)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.videoLocalizedNameFetcher = videoLocalizedNameFetcher ?? throw new ArgumentNullException(nameof(videoLocalizedNameFetcher));
    }

    /// <summary>
    /// Unified method for fetching flat-track publications (Music and Video).
    /// Handles both MP3 (Music) and MP4 (Video) formats with their specific behaviors.
    /// </summary>
    public async Task<bool> FetchFlatPublicationTracksAsync(FetchFlatPublicationTracksRequest request)
    {
        var db = request.Db;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var normalizedLanguageCode = request.NormalizedLanguageCode;
        var englishPublication = request.EnglishPublication;
        var isVideo = request.IsVideo;
        var isMusic = request.IsMusic;
        var fileFormat = request.FileFormat;
        var language = request.Language;
        var cancellationToken = request.CancellationToken;

        string? localizedPubName = null;
        if (isVideo && !normalizedLanguageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            localizedPubName = await videoLocalizedNameFetcher.FetchVideoLocalizedNameFromMediatorAsync(
                normalizedPublicationCode, normalizedLanguageCode, cancellationToken);
        }

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants().ToList();
        var (tracks, fetchedPubName) = await TryFetchAllTracksInOneRequestAsync(
            baseUrls, normalizedPublicationCode, normalizedLanguageCode, fileFormat, cancellationToken);
        if (fetchedPubName != null)
            localizedPubName = fetchedPubName;

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks returned for flat publication {PublicationCode} in language {LanguageCode} (fetch-all, no track param)",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        // Get or create language and category
        Language? resolvedLanguage = language;
        if (resolvedLanguage == null)
        {
            resolvedLanguage = await db.Languages
                .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);
            
            if (resolvedLanguage == null)
            {
                logger.Warning("Language {LanguageCode} not found in database", normalizedLanguageCode);
                return false;
            }
        }

        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(normalizedPublicationCode);
        var categories = await db.Categories
            .Where(c => categoryCodes.Contains(c.CategoryCode))
            .ToListAsync(cancellationToken);
        if (categories.Count == 0)
        {
            logger.Warning("No categories found for publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        BiblePublication? existingPublication;
        if (resolvedLanguage != null)
        {
            existingPublication = await db.BiblePublications
                .Include(bp => bp.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == resolvedLanguage.Id,
                    cancellationToken);
        }
        else
        {
            existingPublication = await db.BiblePublications
                .Include(bp => bp.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == null,
                    cancellationToken);
        }

        if (existingPublication != null)
        {
            logger.Information("Publication {PublicationCode} already exists for language {LanguageCode}, updating tracks and categories",
                normalizedPublicationCode, normalizedLanguageCode ?? "(null)");
            foreach (var track in existingPublication.Tracks)
            {
                if (track.TrackUrl != null)
                {
                    db.TrackUrls.Remove(track.TrackUrl);
                }
            }
            db.BiblePublicationTracks.RemoveRange(existingPublication.Tracks);
            existingPublication.Tracks.Clear();
            existingPublication.Name = localizedPubName ?? englishPublication.Name;
            existingPublication.IsVideo = isVideo;
            existingPublication.IsMusic = isMusic || categories.Any(c => c.CategoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase)) ||
                JwSourceHelper.MusicFlagPublicationCodes.Contains(normalizedPublicationCode);
            SyncPublicationCategories(existingPublication, categories);
            foreach (var track in tracks)
            {
                track.Publication = existingPublication;
                track.BiblePublicationId = existingPublication.Id;
                existingPublication.Tracks.Add(track);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var publicationName = localizedPubName ?? englishPublication.Name;
            var isMusicCategory = categories.Any(c => c.CategoryCode.Equals("Music", StringComparison.OrdinalIgnoreCase)) ||
                JwSourceHelper.MusicFlagPublicationCodes.Contains(normalizedPublicationCode);
            var publication = new BiblePublication
            {
                PublicationCode = normalizedPublicationCode,
                Name = publicationName,
                Language = resolvedLanguage,
                BiblePublicationCategories = categories
                    .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                    .ToList(),
                LanguageId = resolvedLanguage?.Id,
                IsVideo = isVideo,
                IsMusic = isMusic || isMusicCategory,
                CatalogType = CatalogType.Flat,
                Tracks = tracks,
                Sections = new List<BiblePublicationSection>()
            };
            foreach (var track in tracks)
            {
                track.Publication = publication;
            }
            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Information("Successfully fetched {Count} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }

    private static void SyncPublicationCategories(BiblePublication publication, List<Category> categories)
    {
        var existingCategoryIds = publication.BiblePublicationCategories
            .Select(bpc => bpc.CategoryId)
            .ToHashSet();
        foreach (var cat in categories)
        {
            if (existingCategoryIds.Add(cat.Id))
            {
                publication.BiblePublicationCategories.Add(
                    new BiblePublicationCategory { BiblePublicationId = publication.Id, CategoryId = cat.Id, Category = cat });
            }
        }
    }

    /// <summary>
    /// Fetches all tracks in one request (no track param). GETPUBMEDIALINKS returns all tracks when track is omitted.
    /// </summary>
    private async Task<(List<BiblePublicationTrack> Tracks, string? LocalizedPubName)> TryFetchAllTracksInOneRequestAsync(
        List<string> baseUrls,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        string fileFormat,
        CancellationToken cancellationToken)
    {
        var queryString = $"?output=json&pub={normalizedPublicationCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        try
        {
            var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
            if (jsonString == null)
            {
                return (new List<BiblePublicationTrack>(), null);
            }
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement) ||
                !filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty(fileFormat, out var formatFiles) ||
                formatFiles.ValueKind != JsonValueKind.Array)
            {
                return (new List<BiblePublicationTrack>(), null);
            }

            string? fetchedPubName = null;
            if (root.TryGetProperty("pubName", out var pubNameElement))
            {
                var rawName = pubNameElement.GetString();
                fetchedPubName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            var byTrack = new Dictionary<int, JsonElement>();
            foreach (var file in formatFiles.EnumerateArray())
            {
                if (!file.TryGetProperty("track", out var trackEl))
                {
                    continue;
                }

                var trackNum = trackEl.GetInt32();
                if (trackNum == 0)
                {
                    continue;
                }

                if (!byTrack.TryGetValue(trackNum, out var existing))
                {
                    byTrack[trackNum] = file;
                    continue;
                }

                var prefer240p = file.TryGetProperty("label", out var labelEl) && labelEl.GetString() == "240p";
                if (prefer240p)
                {
                    byTrack[trackNum] = file;
                }
            }

            var result = new List<BiblePublicationTrack>();
            foreach (var kv in byTrack.OrderBy(x => x.Key))
            {
                var fileElement = kv.Value;
                if (!fileElement.TryGetProperty("file", out var fileInfo) ||
                    !fileInfo.TryGetProperty("url", out var urlElement))
                {
                    continue;
                }

                var url = urlElement.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                string title = "Unknown";
                if (fileElement.TryGetProperty("title", out var titleElement))
                {
                    var rawTitle = titleElement.GetString();
                    title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                }

                if (AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(normalizedLanguageCode, title))
                {
                    continue;
                }

                result.Add(new BiblePublicationTrack
                {
                    TrackCode = kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Title = title,
                    TrackUrl = new TrackUrl { Url = url }
                });
            }

            if (result.Count > 0)
            {
                logger.Debug("Fetched {Count} video tracks in one request for publication {PublicationCode} in language {LanguageCode}",
                    result.Count, normalizedPublicationCode, normalizedLanguageCode);
            }

            return (result, fetchedPubName);
        }
        catch (Exception ex)
        {
            if (NetworkExceptionHelper.IsNetworkFailure(ex))
            {
                throw;
            }

            logger.Debug(ex, "Fetch-all (no track param) failed for publication {PublicationCode}", normalizedPublicationCode);
            return (new List<BiblePublicationTrack>(), null);
        }
    }
}
