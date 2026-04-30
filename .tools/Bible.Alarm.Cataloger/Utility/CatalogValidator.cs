#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Cataloger.Utility;

/// <summary>
/// Validates cataloged media index by fetching sample API URLs and comparing with DB (publication name, track title, section name).
/// </summary>
internal static class CatalogValidator
{
    public static async Task ValidateAsync(MediaDbContext db, HttpClient httpClient, ILogger logger)
    {
        logger.Information("=== Validating catalog: sample track/section/publication vs API ===");

        var samples = await GetSampleTracksAsync(db, logger);
        var passed = 0;
        var failed = 0;

        foreach (var s in samples)
        {
            var fullUrl = s.TrackUrl;
            if (string.IsNullOrEmpty(fullUrl))
            {
                logger.Warning("CatalogValidator: No track URL for {PublicationCode} track {TrackCode}, skipping", s.PublicationCode, s.TrackCode);
                failed++;
                continue;
            }
            try
            {
                // TrackUrl.Url is now a CDN URL (mp3/mp4); GET returns binary, not JSON. Just validate reachability.
                var request = new HttpRequestMessage(HttpMethod.Head, fullUrl);
                var response = await httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    response.Dispose();
                    response = await httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead);
                }
                response.EnsureSuccessStatusCode();
                response.Dispose();
                logger.Debug("CatalogValidator: OK {PublicationCode} track {TrackCode} | CDN URL reachable", s.PublicationCode, s.TrackCode);
                passed++;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "CatalogValidator: FAIL {PublicationCode} track {TrackCode} | URL: {Url}", s.PublicationCode, s.TrackCode, fullUrl);
                failed++;
            }
        }

        logger.Information("CatalogValidator: Done. Passed: {Passed}, Failed: {Failed}", passed, failed);
    }

    private static async Task<List<SampleTrack>> GetSampleTracksAsync(MediaDbContext db, ILogger logger)
    {
        var samples = new List<SampleTrack>();

        var tracksWithParams = await db.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.TrackUrl)
            .Include(t => t.Publication)
            .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Where(t => t.TrackUrl != null)
            .ToListAsync();

        var byPub = tracksWithParams
            .Where(t => t.Publication != null &&
                        (t.Publication.Language?.LanguageCode == AppConstants.Media.DefaultLanguageCode || t.Publication.LanguageId == null))
            .GroupBy(t => t.Publication!.PublicationCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        void TryAdd(string pubCode, string? sectionCode, string? trackCodeHint)
        {
            if (byPub.TryGetValue(pubCode, out var list))
            {
                var track = list.FirstOrDefault(t =>
                    (string.IsNullOrEmpty(sectionCode) || t.Section?.SectionCode == sectionCode) &&
                    (string.IsNullOrEmpty(trackCodeHint) || t.TrackCode == trackCodeHint || t.TrackCode.StartsWith(trackCodeHint + "-", StringComparison.Ordinal)));
                if (track == null)
                {
                    track = list[0];
                }

                samples.Add(new SampleTrack(
                    track.Publication!.PublicationCode,
                    track.Publication.Name,
                    track.TrackCode,
                    track.Title ?? string.Empty,
                    track.Section?.Name,
                    track.TrackUrl?.Url));
            }
        }

        TryAdd(AppConstants.Media.BiblePublicationCodeNwt, AppConstants.Media.BiblePublicationGenesisBookNumber, AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.BiblePublicationCodeBi12, AppConstants.Media.BiblePublicationGenesisBookNumber, AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.MusicPublicationCodeSjjc, null, AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.MusicPublicationCodeOsg, null, AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.MelodyMusicPublicationCodeIam, $"{AppConstants.Media.MelodyMusicPublicationCodeIam}-1", AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.BiblePublicationCodeDramasGoodNews, null, AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.SeriesPublicationCodeThv, null, AppConstants.Media.BiblePublicationGenesisBookNumber);
        TryAdd(AppConstants.Media.BiblePublicationCategoryDramas, null, null);
        TryAdd(AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures, null, null);
        TryAdd(AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes, null, null);

        if (samples.Count == 0)
        {
            logger.Warning("CatalogValidator: No sample tracks found in DB (no tracks with TrackUrl for language E)");
        }

        return samples;
    }

    private sealed record SampleTrack(
        string PublicationCode,
        string? PublicationName,
        string TrackCode,
        string? TrackTitle,
        string? SectionName,
        string? TrackUrl);

    /// <summary>
    /// Validates that after English seeding each publication has &gt;0 tracks, and if sectioned also &gt;0 sections.
    /// Returns true if all pass, false if any fail (cataloger should exit with code 1).
    /// </summary>
    public static async Task<bool> ValidateEnglishSeedContentAsync(MediaDbContext db, ILogger logger, IReadOnlySet<string>? publicationFilter = null)
    {
        logger.Information("=== Validating E seed content: each pub must have >0 tracks; if sectioned, >0 sections ===");

        var failed = new List<string>();
        var sectionedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppConstants.Media.BiblePublicationCodeNwt,
            AppConstants.Media.BiblePublicationCodeBi12,
            AppConstants.Media.MelodyMusicPublicationCodeIam
        };
        var codesToValidate = publicationFilter != null
            ? publicationFilter
            : (IEnumerable<string>)JwSourceHelper.AllPublicationCodesForEnglishSeeding;

        foreach (var publicationCode in codesToValidate)
        {
            var codeForDb = JwSourceHelper.GetCanonicalMediatorPublicationCode(publicationCode.ToLowerInvariant()) ?? publicationCode;

            // Magazine publications with no discovered issues (future years, empty years) are expected to have no content
            if (MagazineHelper.IsMagazinePublicationCode(publicationCode))
            {
                var hasDiscoveredSections = await db.SectionLanguages
                    .AsNoTracking()
                    .AnyAsync(sl => sl.PublicationCode == codeForDb);
                if (!hasDiscoveredSections)
                {
                    logger.Debug("CatalogValidator E-seed: Skipping magazine {PublicationCode} (no issues discovered)", publicationCode);
                    continue;
                }
            }

            var pub = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                .Include(bp => bp.Tracks)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == codeForDb &&
                    bp.LanguageId != null &&
                    bp.Language != null &&
                    bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

            if (pub == null)
            {
                logger.Warning("CatalogValidator E-seed: Publication {PublicationCode} has no E content (missing)", publicationCode);
                failed.Add($"{publicationCode} (missing)");
                continue;
            }

            var trackCount = pub.Tracks?.Count ?? 0;
            var sectionCount = pub.Sections?.Count ?? 0;

            if (trackCount == 0)
            {
                logger.Warning("CatalogValidator E-seed: Publication {PublicationCode} has 0 tracks", publicationCode);
                failed.Add($"{publicationCode} (0 tracks)");
                continue;
            }

            if (sectionedCodes.Contains(publicationCode) && sectionCount == 0)
            {
                logger.Warning("CatalogValidator E-seed: Publication {PublicationCode} is sectioned but has 0 sections", publicationCode);
                failed.Add($"{publicationCode} (0 sections)");
            }
        }

        var checkIam = publicationFilter == null || publicationFilter.Contains(AppConstants.Media.MelodyMusicPublicationCodeIam);
        if (checkIam)
        {
            var iamCode = AppConstants.Media.MelodyMusicPublicationCodeIam;
            var iamPub = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Sections)
                .Include(bp => bp.Tracks)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == iamCode && bp.LanguageId == null);

            if (iamPub == null)
            {
                logger.Warning("CatalogValidator E-seed: Publication {PublicationCode} (no-language) has no row", iamCode);
                failed.Add($"{iamCode} (missing)");
            }
            else
            {
                var iamTracks = iamPub.Tracks?.Count ?? 0;
                var iamSections = iamPub.Sections?.Count ?? 0;
                if (iamTracks == 0)
                {
                    logger.Warning("CatalogValidator E-seed: Publication {PublicationCode} has 0 tracks", iamCode);
                    failed.Add($"{iamCode} (0 tracks)");
                }
                else if (iamSections == 0)
                {
                    logger.Warning("CatalogValidator E-seed: Publication {PublicationCode} is sectioned but has 0 sections", iamCode);
                    failed.Add($"{iamCode} (0 sections)");
                }
            }
        }

        if (failed.Count == 0)
        {
            logger.Information("CatalogValidator E-seed: All publications have >0 tracks (and >0 sections where required).");
            return true;
        }

        logger.Warning("CatalogValidator E-seed: {Count} publication(s) failed validation: {Failed}", failed.Count, string.Join(", ", failed));
        return false;
    }

    /// <summary>
    /// Validates each mediator category URL: GET /categories/E/{code} and asserts category.media exists (array).
    /// Returns true if all pass, false if any fail (cataloger should exit with code 1 when run before catalog).
    /// </summary>
    public static async Task<bool> ValidateMediatorLinksAsync(ILogger logger)
    {
        logger.Information("=== Validating mediator links: each category must return category.media array ===");

        var failed = new List<string>();
        var codesToValidate = JwSourceHelper.AllMediatorPublicationCodes
            .Where(c => !JwSourceHelper.MediatorValidationExclusionCodes.Contains(c))
            .ToList();
        foreach (var publicationCode in codesToValidate)
        {
            var categoryKey = JwSourceHelper.GetMediatorCategoryKey(publicationCode);
            var pathAndQuery = $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/{AppConstants.Media.DefaultLanguageCode}/{categoryKey}";
            string? jsonString;
            try
            {
                jsonString = await DownloadUtility.GetMediatorAsync(pathAndQuery);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "CatalogValidator mediator: Failed to fetch {PublicationCode}", publicationCode);
                failed.Add($"{publicationCode} (fetch error)");
                continue;
            }

            if (string.IsNullOrEmpty(jsonString))
            {
                logger.Warning("CatalogValidator mediator: Empty response for {PublicationCode}", publicationCode);
                failed.Add($"{publicationCode} (empty response)");
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category))
                {
                    logger.Warning("CatalogValidator mediator: No 'category' in response for {PublicationCode}", publicationCode);
                    failed.Add($"{publicationCode} (no category)");
                    continue;
                }

                if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.CategoryMedia, out var mediaEl) || mediaEl.ValueKind != JsonValueKind.Array)
                {
                    logger.Warning("CatalogValidator mediator: No 'category.media' array for {PublicationCode}", publicationCode);
                    failed.Add($"{publicationCode} (no category.media array)");
                    continue;
                }

                logger.Debug("CatalogValidator mediator: OK {PublicationCode} (media count: {Count})", publicationCode, mediaEl.GetArrayLength());
            }
            catch (JsonException ex)
            {
                logger.Warning(ex, "CatalogValidator mediator: Invalid JSON for {PublicationCode}", publicationCode);
                failed.Add($"{publicationCode} (invalid JSON)");
            }
        }

        if (failed.Count == 0)
        {
            logger.Information("CatalogValidator mediator: All {Count} mediator links returned valid category.media.", codesToValidate.Count);
            return true;
        }

        logger.Warning("CatalogValidator mediator: {Count} link(s) failed: {Failed}", failed.Count, string.Join(", ", failed));
        return false;
    }
}
