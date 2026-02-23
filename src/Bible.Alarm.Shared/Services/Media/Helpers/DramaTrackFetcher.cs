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
/// Helper class for fetching drama tracks from GETPUBMEDIALINKS API.
/// </summary>
internal sealed class DramaTrackFetcher
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly DramaTrackParser trackParser;

    public DramaTrackFetcher(HttpClient httpClient, ILogger logger, DramaTrackParser trackParser)
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

        logger.Information("DramaTrackFetcher: Processing {ItemCount} media items for publication {PublicationCode} in language {LanguageCode}",
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
                    logger.Debug("DramaTrackFetcher: Fetched track {SectionCode}-{TrackNumber}",
                        sectionCode, trackNumber);
                }
                else
                {
                    failed++;
                    logger.Warning("DramaTrackFetcher: No track found for {SectionCode} track {TrackNumber}",
                        sectionCode, trackNumber);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                failed++;
                logger.Debug("DramaTrackFetcher: {SectionCode} track {TrackNumber} not available for {PublicationCode} in {LanguageCode}",
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
                logger.Warning(ex, "DramaTrackFetcher: Failed to fetch {SectionCode} track {TrackNumber} for {PublicationCode} in {LanguageCode}",
                    sectionCode, trackNumber, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
        }

        logger.Information("DramaTrackFetcher: Completed. Successful: {SuccessfulCount}, Failed: {FailedCount}, Total tracks: {TrackCount}",
            successful, failed, allTracks.Count);

        return allTracks;
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
        var queryString = $"?output=json&pub={sectionCode}&track={trackNumber}&fileformat={fileFormat}&alllangs=0&langwritten={normalizedLanguageCode}";
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
        return trackParser.ParseTracksFromJson(
            sectionFilesElement, normalizedLanguageCode, sectionCode, isVideo, trackNumber, allowAudioDescriptionTitles);
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
            logger.Information("DramaTrackFetcher: Sequential fetch for {SectionCode} returned {TrackCount} tracks (track 1..{LastTrack})",
                sectionCode, allTracks.Count, trackNumber - 1);
        }

        return allTracks;
    }
}
