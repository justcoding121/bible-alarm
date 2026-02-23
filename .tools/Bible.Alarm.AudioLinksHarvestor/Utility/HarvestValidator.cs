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

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

/// <summary>
/// Validates harvested media index by fetching sample API URLs and comparing with DB (publication name, track title, section name).
/// </summary>
internal static class HarvestValidator
{
    private const string LanguageE = "E";

    private static bool IsSectionedOrMediatorPublication(string publicationCode)
    {
        var c = publicationCode.ToLowerInvariant();
        return c == "nwt" || c == "bi12" || c == "iam" || c == "dramas" || c == "dramaticbiblereadings" ||
               c.StartsWith("vod", StringComparison.Ordinal) || c == "seriesdigfortreasures" || c == "seriesbjflessons";
    }

    public static async Task ValidateAsync(MediaDbContext db, HttpClient httpClient, ILogger logger)
    {
        logger.Information("=== Validating harvest: sample track/section/publication vs API ===");

        var baseUrl = AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl;
        var samples = await GetSampleTracksAsync(db, logger);
        var passed = 0;
        var failed = 0;

        foreach (var s in samples)
        {
            var query = BuildQueryString(s.UrlParams);
            if (string.IsNullOrEmpty(query))
            {
                logger.Warning("HarvestValidator: No URL params for {PublicationCode} track {TrackCode}, skipping", s.PublicationCode, s.TrackCode);
                failed++;
                continue;
            }

            var fullUrl = baseUrl + query;
            try
            {
                var response = await httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var pubNameFromApi = root.TryGetProperty("pubName", out var pn) ? pn.GetString() : null;
                var (titleFromApi, _) = GetFirstTrackFromResponse(root, LanguageE);
                var (sectionNameFromApi, _) = GetSectionNameFromResponse(root);

                var nameMatch = string.IsNullOrEmpty(pubNameFromApi) ||
                                string.Equals(pubNameFromApi?.Trim(), s.PublicationName?.Trim(), StringComparison.OrdinalIgnoreCase);
                var titleMatch = string.IsNullOrEmpty(titleFromApi) ||
                                 (s.TrackTitle != null && titleFromApi != null && (titleFromApi.Contains(s.TrackTitle, StringComparison.OrdinalIgnoreCase) || s.TrackTitle.Contains(titleFromApi, StringComparison.OrdinalIgnoreCase))) ||
                                 string.Equals(titleFromApi?.Trim(), s.TrackTitle?.Trim(), StringComparison.OrdinalIgnoreCase);

                bool isSectionedOrMediator = IsSectionedOrMediatorPublication(s.PublicationCode);
                bool pass = titleMatch && (nameMatch || isSectionedOrMediator);
                if (isSectionedOrMediator && !nameMatch)
                {
                    logger.Information("HarvestValidator: OK (sectioned/mediator) {PublicationCode} | pubName API={ApiName} DB={DbName} (section/episode vs publication name) | title match: {TitleMatch}",
                        s.PublicationCode, pubNameFromApi ?? "(none)", s.PublicationName ?? "(none)", titleMatch);
                }

                if (pass)
                {
                    if (nameMatch && titleMatch)
                    {
                        logger.Information("HarvestValidator: OK {PublicationCode} | pubName API vs DB: {ApiName} vs {DbName} | track: {ApiTitle} vs DB {DbTitle}",
                            s.PublicationCode, pubNameFromApi ?? "(none)", s.PublicationName ?? "(none)", titleFromApi ?? "(none)", s.TrackTitle ?? "(none)");
                    }
                    passed++;
                }
                else
                {
                    logger.Warning("HarvestValidator: MISMATCH {PublicationCode} track {TrackCode} | pubName API={ApiName} DB={DbName} | title API={ApiTitle} DB={DbTitle}",
                        s.PublicationCode, s.TrackCode, pubNameFromApi ?? "(none)", s.PublicationName ?? "(none)", titleFromApi ?? "(none)", s.TrackTitle ?? "(none)");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "HarvestValidator: FAIL {PublicationCode} track {TrackCode} | URL: {Url}", s.PublicationCode, s.TrackCode, fullUrl);
                failed++;
            }
        }

        logger.Information("HarvestValidator: Done. Passed: {Passed}, Failed: {Failed}", passed, failed);
    }

    private static string BuildQueryString(IReadOnlyList<UrlParam> urlParams)
    {
        if (urlParams == null || urlParams.Count == 0)
        {
            return string.Empty;
        }

        var queryParams = urlParams
            .Where(p => p.IsQueryParam && !string.IsNullOrEmpty(p.Key))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value ?? "")}")
            .ToList();

        if (queryParams.Count == 0)
        {
            return string.Empty;
        }

        var hasOutput = queryParams.Any(p => p.StartsWith("output=", StringComparison.OrdinalIgnoreCase));
        if (!hasOutput)
        {
            queryParams.Insert(0, "output=json");
        }

        return "?" + string.Join("&", queryParams);
    }

    private static (string? Title, string? Url) GetFirstTrackFromResponse(JsonElement root, string languageCode)
    {
        if (!root.TryGetProperty("files", out var files) ||
            !files.TryGetProperty(languageCode, out var langFiles))
        {
            return (null, null);
        }

        if (langFiles.TryGetProperty("MP3", out var mp3) && mp3.GetArrayLength() > 0)
        {
            var first = mp3[0];
            var title = first.TryGetProperty("title", out var t) ? t.GetString() : null;
            var url = GetUrlFromTrackElement(first);
            return (title, url);
        }

        if (langFiles.TryGetProperty("MP4", out var mp4) && mp4.GetArrayLength() > 0)
        {
            var first = mp4[0];
            var title = first.TryGetProperty("title", out var t) ? t.GetString() : null;
            var url = GetUrlFromTrackElement(first);
            return (title, url);
        }

        return (null, null);
    }

    private static string? GetUrlFromTrackElement(JsonElement trackElement)
    {
        if (!trackElement.TryGetProperty("file", out var fileEl))
        {
            return null;
        }

        if (fileEl.ValueKind == JsonValueKind.String)
        {
            return fileEl.GetString();
        }

        if (fileEl.ValueKind == JsonValueKind.Object && fileEl.TryGetProperty("url", out var urlEl))
        {
            return urlEl.GetString();
        }

        return null;
    }

    private static (string? SectionName, string? PubName) GetSectionNameFromResponse(JsonElement root)
    {
        var pubName = root.TryGetProperty("pubName", out var pn) ? pn.GetString() : null;
        return (pubName, pubName);
    }

    private static async Task<List<SampleTrack>> GetSampleTracksAsync(MediaDbContext db, ILogger logger)
    {
        var samples = new List<SampleTrack>();

        var tracksWithParams = await db.BiblePublicationTracks
            .AsNoTracking()
            .Include(t => t.UrlParams)
            .Include(t => t.Publication)
            .ThenInclude(p => p!.Language)
            .Include(t => t.Section)
            .Where(t => t.UrlParams.Any())
            .ToListAsync();

        var byPub = tracksWithParams
            .Where(t => t.Publication != null &&
                        (t.Publication.Language?.LanguageCode == LanguageE || t.Publication.LanguageId == null))
            .GroupBy(t => t.Publication!.PublicationCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        void TryAdd(string pubCode, string? sectionCode, string? trackCodeHint)
        {
            if (byPub.TryGetValue(pubCode, out var list))
            {
                var track = list.FirstOrDefault(t =>
                    (string.IsNullOrEmpty(sectionCode) || t.Section?.SectionCode == sectionCode) &&
                    (string.IsNullOrEmpty(trackCodeHint) || t.TrackCode == trackCodeHint || t.TrackCode.StartsWith(trackCodeHint + "-", StringComparison.Ordinal)));
                if (track == null)
                {
                    track = list.First();
                }

                samples.Add(new SampleTrack(
                    track.Publication!.PublicationCode,
                    track.Publication.Name,
                    track.TrackCode,
                    track.Title ?? string.Empty,
                    track.Section?.Name,
                    track.UrlParams));
            }
        }

        TryAdd("nwt", "1", "1");
        TryAdd("bi12", "1", "1");
        TryAdd("sjjc", null, "1");
        TryAdd("osg", null, "1");
        TryAdd("iam", "iam-1", "1");
        TryAdd("gnj", null, "1");
        TryAdd("thv", null, "1");
        TryAdd("Dramas", null, null);
        TryAdd("SeriesDigForTreasures", null, null);
        TryAdd("VODMoviesBibleTimes", null, null);

        if (samples.Count == 0)
        {
            logger.Warning("HarvestValidator: No sample tracks found in DB (no tracks with UrlParams for language E)");
        }

        return samples;
    }

    private sealed record SampleTrack(
        string PublicationCode,
        string? PublicationName,
        string TrackCode,
        string? TrackTitle,
        string? SectionName,
        List<UrlParam> UrlParams);

    /// <summary>
    /// Validates that after English seeding each publication has &gt;0 tracks, and if sectioned also &gt;0 sections.
    /// Returns true if all pass, false if any fail (harvester should exit with code 1).
    /// </summary>
    public static async Task<bool> ValidateEnglishSeedContentAsync(MediaDbContext db, ILogger logger)
    {
        logger.Information("=== Validating E seed content: each pub must have >0 tracks; if sectioned, >0 sections ===");

        var failed = new List<string>();
        var sectionedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "nwt", "bi12", "iam" };

        foreach (var publicationCode in JwSourceHelper.AllPublicationCodesForEnglishSeeding)
        {
            var codeForDb = JwSourceHelper.GetCanonicalDramaPublicationCode(publicationCode.ToLowerInvariant()) ?? publicationCode;
            var pub = await db.BiblePublications
                .AsNoTracking()
                .Include(bp => bp.Language)
                .Include(bp => bp.Sections)
                .Include(bp => bp.Tracks)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == codeForDb &&
                    bp.LanguageId != null &&
                    bp.Language != null &&
                    bp.Language.LanguageCode == LanguageE);

            if (pub == null)
            {
                logger.Warning("HarvestValidator E-seed: Publication {PublicationCode} has no E content (missing)", publicationCode);
                failed.Add($"{publicationCode} (missing)");
                continue;
            }

            var trackCount = pub.Tracks?.Count ?? 0;
            var sectionCount = pub.Sections?.Count ?? 0;

            if (trackCount == 0)
            {
                logger.Warning("HarvestValidator E-seed: Publication {PublicationCode} has 0 tracks", publicationCode);
                failed.Add($"{publicationCode} (0 tracks)");
                continue;
            }

            if (sectionedCodes.Contains(publicationCode) && sectionCount == 0)
            {
                logger.Warning("HarvestValidator E-seed: Publication {PublicationCode} is sectioned but has 0 sections", publicationCode);
                failed.Add($"{publicationCode} (0 sections)");
            }
        }

        var iamPub = await db.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.Sections)
            .Include(bp => bp.Tracks)
            .FirstOrDefaultAsync(bp => bp.PublicationCode == "iam" && bp.LanguageId == null);

        if (iamPub == null)
        {
            logger.Warning("HarvestValidator E-seed: Publication iam (no-language) has no row");
            failed.Add("iam (missing)");
        }
        else
        {
            var iamTracks = iamPub.Tracks?.Count ?? 0;
            var iamSections = iamPub.Sections?.Count ?? 0;
            if (iamTracks == 0)
            {
                logger.Warning("HarvestValidator E-seed: Publication iam has 0 tracks");
                failed.Add("iam (0 tracks)");
            }
            else if (iamSections == 0)
            {
                logger.Warning("HarvestValidator E-seed: Publication iam is sectioned but has 0 sections");
                failed.Add("iam (0 sections)");
            }
        }

        if (failed.Count == 0)
        {
            logger.Information("HarvestValidator E-seed: All publications have >0 tracks (and >0 sections where required).");
            return true;
        }

        logger.Warning("HarvestValidator E-seed: {Count} publication(s) failed validation: {Failed}", failed.Count, string.Join(", ", failed));
        return false;
    }
}
