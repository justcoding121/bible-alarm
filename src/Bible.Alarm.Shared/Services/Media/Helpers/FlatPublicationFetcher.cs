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

    private sealed class ExistingFlatPublicationUpdateContext
    {
        public required MediaDbContext Db { get; init; }
        public required BiblePublication ExistingPublication { get; init; }
        public required List<BiblePublicationTrack> Tracks { get; init; }
        public string? LocalizedPubName { get; init; }
        public required string EnglishPublicationName { get; init; }
        public required bool IsVideo { get; init; }
        public required bool IsMusic { get; init; }
        public required List<Category> Categories { get; init; }
        public required string NormalizedPublicationCode { get; init; }
        public required string NormalizedLanguageCode { get; init; }
        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class NewFlatPublicationInsertContext
    {
        public required MediaDbContext Db { get; init; }
        public required string NormalizedPublicationCode { get; init; }
        public string? LocalizedPubName { get; init; }
        public required string EnglishPublicationName { get; init; }
        public required Language ResolvedLanguage { get; init; }
        public required bool IsVideo { get; init; }
        public required bool IsMusic { get; init; }
        public required List<Category> Categories { get; init; }
        public required List<BiblePublicationTrack> Tracks { get; init; }
        public required CancellationToken CancellationToken { get; init; }
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

        var localizedPubName = await TryGetVideoLocalizedPublicationNameAsync(
            isVideo, normalizedPublicationCode, normalizedLanguageCode, cancellationToken);

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants().ToList();
        var (tracks, fetchedPubName) = await TryFetchAllTracksInOneRequestAsync(
            baseUrls, normalizedPublicationCode, normalizedLanguageCode, fileFormat, cancellationToken);

        if (fetchedPubName != null)
        {
            localizedPubName = fetchedPubName;
        }

        if (tracks.Count == 0)
        {
            logger.Warning("No tracks returned for flat publication {PublicationCode} in language {LanguageCode} (fetch-all, no track param)",
                normalizedPublicationCode, normalizedLanguageCode);
            return false;
        }

        var resolvedLanguage = await ResolveLanguageForRequestAsync(db, language, normalizedLanguageCode, cancellationToken);
        if (resolvedLanguage == null)
        {
            return false;
        }

        var categories = await LoadCategoriesForPublicationAsync(db, normalizedPublicationCode, cancellationToken);
        if (categories.Count == 0)
        {
            logger.Warning("No categories found for publication {PublicationCode}", normalizedPublicationCode);
            return false;
        }

        var existingPublication = await FindExistingPublicationAsync(db, normalizedPublicationCode, resolvedLanguage, cancellationToken);

        if (existingPublication != null)
        {
            await UpdateExistingPublicationTracksAsync(new ExistingFlatPublicationUpdateContext
            {
                Db = db,
                ExistingPublication = existingPublication,
                Tracks = tracks,
                LocalizedPubName = localizedPubName,
                EnglishPublicationName = englishPublication.Name,
                IsVideo = isVideo,
                IsMusic = isMusic,
                Categories = categories,
                NormalizedPublicationCode = normalizedPublicationCode,
                NormalizedLanguageCode = normalizedLanguageCode,
                CancellationToken = cancellationToken
            });
        }
        else
        {
            await InsertNewFlatPublicationAsync(new NewFlatPublicationInsertContext
            {
                Db = db,
                NormalizedPublicationCode = normalizedPublicationCode,
                LocalizedPubName = localizedPubName,
                EnglishPublicationName = englishPublication.Name,
                ResolvedLanguage = resolvedLanguage,
                IsVideo = isVideo,
                IsMusic = isMusic,
                Categories = categories,
                Tracks = tracks,
                CancellationToken = cancellationToken
            });
        }

        logger.Information("Successfully fetched {Count} tracks for publication {PublicationCode} in language {LanguageCode}",
            tracks.Count, normalizedPublicationCode, normalizedLanguageCode);

        return true;
    }

    private async Task<string?> TryGetVideoLocalizedPublicationNameAsync(
        bool isVideo,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        if (!isVideo || normalizedLanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await videoLocalizedNameFetcher.FetchVideoLocalizedNameFromMediatorAsync(
            normalizedPublicationCode, normalizedLanguageCode, cancellationToken);
    }

    private async Task<Language?> ResolveLanguageForRequestAsync(
        MediaDbContext db,
        Language? language,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        if (language != null)
        {
            return language;
        }

        var resolved = await db.Languages
            .FirstOrDefaultAsync(l => l.LanguageCode == normalizedLanguageCode, cancellationToken);

        if (resolved == null)
        {
            logger.Warning("Language {LanguageCode} not found in database", normalizedLanguageCode);
        }

        return resolved;
    }

    private static async Task<List<Category>> LoadCategoriesForPublicationAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        CancellationToken cancellationToken)
    {
        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(normalizedPublicationCode);
        return await db.Categories
            .Where(c => categoryCodes.Contains(c.CategoryCode))
            .ToListAsync(cancellationToken);
    }

    private static async Task<BiblePublication?> FindExistingPublicationAsync(
        MediaDbContext db,
        string normalizedPublicationCode,
        Language? resolvedLanguage,
        CancellationToken cancellationToken)
    {
        if (resolvedLanguage != null)
        {
            return await db.BiblePublications
                .Include(bp => bp.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == normalizedPublicationCode &&
                          bp.LanguageId == resolvedLanguage.Id,
                    cancellationToken);
        }

        return await db.BiblePublications
            .Include(bp => bp.Tracks)
            .ThenInclude(t => t.TrackUrl)
            .Include(bp => bp.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .FirstOrDefaultAsync(
                bp => bp.PublicationCode == normalizedPublicationCode &&
                      bp.LanguageId == null,
                cancellationToken);
    }

    private static bool ComputePublicationIsMusic(bool isMusic, List<Category> categories, string normalizedPublicationCode)
    {
        return isMusic || categories.Any(c => c.CategoryCode.Equals(AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase)) ||
               JwSourceHelper.MusicFlagPublicationCodes.Contains(normalizedPublicationCode);
    }

    private async Task UpdateExistingPublicationTracksAsync(ExistingFlatPublicationUpdateContext ctx)
    {
        logger.Information("Publication {PublicationCode} already exists for language {LanguageCode}, updating tracks and categories",
            ctx.NormalizedPublicationCode, ctx.NormalizedLanguageCode ?? "(null)");

        foreach (var track in ctx.ExistingPublication.Tracks.Where(t => t.TrackUrl != null))
        {
            ctx.Db.TrackUrls.Remove(track.TrackUrl!);
        }

        ctx.Db.BiblePublicationTracks.RemoveRange(ctx.ExistingPublication.Tracks);
        ctx.ExistingPublication.Tracks.Clear();
        ctx.ExistingPublication.Name = ctx.LocalizedPubName ?? ctx.EnglishPublicationName;
        ctx.ExistingPublication.IsVideo = ctx.IsVideo;
        ctx.ExistingPublication.IsMusic = ComputePublicationIsMusic(ctx.IsMusic, ctx.Categories, ctx.NormalizedPublicationCode);
        SyncPublicationCategories(ctx.ExistingPublication, ctx.Categories);
        foreach (var track in ctx.Tracks)
        {
            track.Publication = ctx.ExistingPublication;
            track.BiblePublicationId = ctx.ExistingPublication.Id;
            ctx.ExistingPublication.Tracks.Add(track);
        }

        await ctx.Db.SaveChangesAsync(ctx.CancellationToken);
    }

    private static async Task InsertNewFlatPublicationAsync(NewFlatPublicationInsertContext ctx)
    {
        var publicationName = ctx.LocalizedPubName ?? ctx.EnglishPublicationName;
        var publication = new BiblePublication
        {
            PublicationCode = ctx.NormalizedPublicationCode,
            Name = publicationName,
            Language = ctx.ResolvedLanguage,
            BiblePublicationCategories = ctx.Categories
                .Select(cat => new BiblePublicationCategory { BiblePublicationId = 0, CategoryId = cat.Id, Category = cat })
                .ToList(),
            LanguageId = ctx.ResolvedLanguage.Id,
            IsVideo = ctx.IsVideo,
            IsMusic = ComputePublicationIsMusic(ctx.IsMusic, ctx.Categories, ctx.NormalizedPublicationCode),
            CatalogType = CatalogType.Flat,
            Tracks = ctx.Tracks,
            Sections = new List<BiblePublicationSection>()
        };

        foreach (var track in ctx.Tracks)
        {
            track.Publication = publication;
        }

        ctx.Db.BiblePublications.Add(publication);
        await ctx.Db.SaveChangesAsync(ctx.CancellationToken);
    }

    private static void SyncPublicationCategories(BiblePublication publication, List<Category> categories)
    {
        var existingCategoryIds = publication.BiblePublicationCategories
            .Select(bpc => bpc.CategoryId)
            .ToHashSet();
        foreach (var cat in categories.Where(c => existingCategoryIds.Add(c.Id)))
        {
            publication.BiblePublicationCategories.Add(
                new BiblePublicationCategory { BiblePublicationId = publication.Id, CategoryId = cat.Id, Category = cat });
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
        var queryString = $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={normalizedPublicationCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={fileFormat}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={normalizedLanguageCode}";
        try
        {
            var jsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
            if (jsonString == null)
            {
                return (new List<BiblePublicationTrack>(), null);
            }

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!TryGetFormatFilesFromPubMediaRoot(root, normalizedLanguageCode, fileFormat, out var formatFiles))
            {
                return (new List<BiblePublicationTrack>(), null);
            }

            var fetchedPubName = TryDecodePublicationNameFromRoot(root);
            var byTrack = BuildPreferredFilesByTrackNumber(formatFiles);
            var result = BuildPublicationTracksFromPreferredFiles(byTrack, normalizedLanguageCode);

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

    private static bool TryGetFormatFilesFromPubMediaRoot(
        JsonElement root,
        string normalizedLanguageCode,
        string fileFormat,
        out JsonElement formatFiles)
    {
        formatFiles = default;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement) ||
            !filesElement.TryGetProperty(normalizedLanguageCode, out var languageFiles) ||
            !languageFiles.TryGetProperty(fileFormat, out formatFiles) ||
            formatFiles.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return true;
    }

    private static string? TryDecodePublicationNameFromRoot(JsonElement root)
    {
        if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            return null;
        }

        var rawName = pubNameElement.GetString();
        return MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
    }

    private static Dictionary<int, JsonElement> BuildPreferredFilesByTrackNumber(JsonElement formatFiles)
    {
        var byTrack = new Dictionary<int, JsonElement>();
        foreach (var file in formatFiles.EnumerateArray())
        {
            if (!file.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var trackEl))
            {
                continue;
            }

            var trackNum = trackEl.GetInt32();
            if (trackNum == 0)
            {
                continue;
            }

            if (!byTrack.TryGetValue(trackNum, out _))
            {
                byTrack[trackNum] = file;
                continue;
            }

            var prefer240p = file.TryGetProperty(AppConstants.Media.PubMediaJson.Label, out var labelEl) &&
                             labelEl.GetString() == AppConstants.Media.VideoQualityLabel240p;
            if (prefer240p)
            {
                byTrack[trackNum] = file;
            }
        }

        return byTrack;
    }

    private static List<BiblePublicationTrack> BuildPublicationTracksFromPreferredFiles(
        Dictionary<int, JsonElement> byTrack,
        string normalizedLanguageCode)
    {
        var result = new List<BiblePublicationTrack>();
        foreach (var kv in byTrack.OrderBy(x => x.Key))
        {
            var fileElement = kv.Value;
            if (!fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileInfo) ||
                !fileInfo.TryGetProperty(AppConstants.Media.PubMediaJson.Url, out var urlElement))
            {
                continue;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            var title = MediaTrackTitleHelper.UnknownTitle;
            if (fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
            {
                title = MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
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

        return result;
    }
}
