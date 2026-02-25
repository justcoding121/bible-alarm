#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media.Helpers;

/// <summary>
/// Helper class for fetching mediator tracks from GETPUBMEDIALINKS API.
/// </summary>
internal sealed class MediatorTrackFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly MediatorTrackParser trackParser;

    public MediatorTrackFetcher(HttpClient httpClient, ILogger logger, MediatorTrackParser trackParser)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.trackParser = trackParser ?? throw new ArgumentNullException(nameof(trackParser));
    }

    public async Task<List<BiblePublicationTrack>> FetchTracksForMediaItemsAsync(
        MediaDbContext db,
        List<(string SectionCode, int TrackNumber)> mediaItems,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        var allTracks = new List<BiblePublicationTrack>();
        var successful = 0;
        var failed = 0;

        logger.Information("MediatorTrackFetcher: Processing {ItemCount} media items for publication {PublicationCode} in language {LanguageCode}",
            mediaItems.Count, normalizedPublicationCode, normalizedLanguageCode);

        foreach (var (sectionCode, trackNumber) in mediaItems)
        {
            try
            {
                var tracks = await FetchSingleTrackAsync(
                    sectionCode, trackNumber, normalizedPublicationCode, normalizedLanguageCode, baseUrls,
                    cancellationToken);

                if (tracks.Count > 0)
                {
                    allTracks.AddRange(tracks);
                    successful++;
                    logger.Debug("MediatorTrackFetcher: Fetched track {SectionCode}-{TrackNumber}",
                        sectionCode, trackNumber);
                }
                else
                {
                    failed++;
                    var url = BuildTrackRequestUrl(sectionCode, trackNumber, normalizedPublicationCode, normalizedLanguageCode);
                    logger.Warning("MediatorTrackFetcher: No track found for {SectionCode} track {TrackNumber}. URL: {Url}",
                        sectionCode, trackNumber, url);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                failed++;
                logger.Debug("MediatorTrackFetcher: {SectionCode} track {TrackNumber} not available for {PublicationCode} in {LanguageCode}",
                    sectionCode, trackNumber, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.IsNetworkFailure(ex))
                {
                    throw;
                }

                failed++;
                logger.Warning(ex, "MediatorTrackFetcher: Failed to fetch {SectionCode} track {TrackNumber} for {PublicationCode} in {LanguageCode}",
                    sectionCode, trackNumber, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
        }

        logger.Information("MediatorTrackFetcher: Completed. Successful: {SuccessfulCount}, Failed: {FailedCount}, Total tracks: {TrackCount}",
            successful, failed, allTracks.Count);

        return allTracks;
    }

    private static string BuildTrackRequestUrl(string sectionCode, int trackNumber, string normalizedPublicationCode, string normalizedLanguageCode)
    {
        var isVideo = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
        var fileFormat = isVideo ? "MP4" : "MP3";
        string queryString;
        if (sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase))
        {
            var docidValue = sectionCode.Substring(6);
            queryString = $"?output=json&docid={docidValue}&track={trackNumber}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else if (JwSourceHelper.SectionCodesSingleTrackNoParam.Contains(sectionCode))
        {
            queryString = $"?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else
        {
            var effectiveTrack = JwSourceHelper.SectionCodesSingleTrackZero.Contains(sectionCode) ? 0 : trackNumber;
            var useIssue = JwSourceHelper.SectionCodesUsingIssueParameter.Contains(sectionCode) || JwSourceHelper.LooksLikeIssueNumber(trackNumber);
            queryString = $"?output=json&pub={sectionCode}&{(useIssue ? "issue" : "track")}={effectiveTrack}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }

        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        return baseUrls.Count > 0 ? baseUrls[0] + queryString : "(no base URL)" + queryString;
    }

    private async Task<List<BiblePublicationTrack>> FetchSingleTrackAsync(
        string sectionCode,
        int trackNumber,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        IReadOnlyList<string> baseUrls,
        CancellationToken cancellationToken)
    {
        var isVideo = PublicationTypeHelper.IsVideo(normalizedPublicationCode);
        var fileFormat = isVideo ? "MP4" : "MP3";
        var useIssueParam = JwSourceHelper.SectionCodesUsingIssueParameter.Contains(sectionCode) || JwSourceHelper.LooksLikeIssueNumber(trackNumber);
        string queryString;
        if (sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase))
        {
            var docidValue = sectionCode.Substring(6);
            queryString = $"?output=json&docid={docidValue}&track={trackNumber}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else if (JwSourceHelper.SectionCodesSingleTrackNoParam.Contains(sectionCode))
        {
            queryString = $"?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }
        else
        {
            var effectiveTrack = JwSourceHelper.SectionCodesSingleTrackZero.Contains(sectionCode) ? 0 : trackNumber;
            queryString = $"?output=json&pub={sectionCode}&{(useIssueParam ? "issue" : "track")}={effectiveTrack}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
        }

        var sectionJsonString = await GetPubMediaLinksRetry.GetStringAsync(httpClient, baseUrls, queryString, cancellationToken);
        if (sectionJsonString == null)
        {
            return new List<BiblePublicationTrack>();
        }

        using var sectionDoc = JsonDocument.Parse(sectionJsonString);
        var sectionRoot = sectionDoc.RootElement;

        if (sectionRoot.ValueKind != JsonValueKind.Object || !sectionRoot.TryGetProperty("files", out var sectionFilesElement))
        {
            return new List<BiblePublicationTrack>();
        }

        var allowAudioDescriptionTitles = normalizedPublicationCode.EndsWith("AD", StringComparison.OrdinalIgnoreCase);
        var useDocidParam = sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase);
        var omitTrackFromUrlParams = !useDocidParam && JwSourceHelper.SectionCodesSingleTrackNoParam.Contains(sectionCode);
        var effectiveTrackForParams = JwSourceHelper.SectionCodesSingleTrackZero.Contains(sectionCode) ? 0 : trackNumber;
        return trackParser.ParseTracksFromJson(
            sectionFilesElement, normalizedLanguageCode, sectionCode, isVideo, effectiveTrackForParams, allowAudioDescriptionTitles, useIssueParam, omitTrackFromUrlParams, useDocidParam);
    }

    /// <summary>
    /// Fetches tracks by requesting track 1, 2, 3, ... until GETPUBMEDIALINKS returns no files.
    /// Used for publications (e.g. VODLFFVideosAD) where the mediator list uses different numbering than the GETPUBMEDIALINKS API (e.g. pub=lffv).
    /// </summary>
    public async Task<List<BiblePublicationTrack>> FetchTracksSequentiallyAsync(
        string sectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        int maxTracks,
        CancellationToken cancellationToken)
    {
        var baseUrls = GetPubMediaLinksRetry.GetBaseUrlsFromConstants();
        var allTracks = new List<BiblePublicationTrack>();
        var trackNumber = 1;

        while (trackNumber <= maxTracks)
        {
            var tracks = await FetchSingleTrackAsync(
                sectionCode, trackNumber, normalizedPublicationCode, normalizedLanguageCode, baseUrls,
                cancellationToken);

            if (tracks.Count == 0)
            {
                break;
            }

            allTracks.AddRange(tracks);
            trackNumber++;
        }

        if (allTracks.Count > 0)
        {
            logger.Information("MediatorTrackFetcher: Sequential fetch for {SectionCode} returned {TrackCount} tracks (track 1..{LastTrack})",
                sectionCode, allTracks.Count, trackNumber - 1);
        }

        return allTracks;
    }
}
