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

    public async Task<List<BiblePublicationTrack>> FetchTracksForSectionsAsync(
        MediaDbContext db,
        HashSet<string> sectionCodes,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        // Get BaseUrl
        var baseUrl = await db.BaseUrls
            .Where(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS")
            .FirstOrDefaultAsync(cancellationToken);

        if (baseUrl == null)
        {
            logger.Warning("No BaseUrl found");
            return new List<BiblePublicationTrack>();
        }

        var allTracks = new List<BiblePublicationTrack>();
        var successfulSections = 0;
        var failedSections = 0;

        logger.Information("DramaTrackFetcher: Processing {SectionCount} sections for publication {PublicationCode} in language {LanguageCode}",
            sectionCodes.Count, normalizedPublicationCode, normalizedLanguageCode);

        foreach (var sectionCode in sectionCodes.OrderBy(sc => sc))
        {
            try
            {
                var tracks = await FetchTracksForSectionAsync(
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode, baseUrl, 
                    cancellationToken);
                
                if (tracks.Count > 0)
                {
                    allTracks.AddRange(tracks);
                    successfulSections++;
                    logger.Debug("DramaTrackFetcher: Fetched {TrackCount} tracks for section {SectionCode}",
                        tracks.Count, sectionCode);
                }
                else
                {
                    failedSections++;
                    logger.Warning("DramaTrackFetcher: No tracks found for section {SectionCode}",
                        sectionCode);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                failedSections++;
                logger.Debug("DramaTrackFetcher: Section {SectionCode} not available for drama {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
            catch (Exception ex)
            {
                failedSections++;
                logger.Warning(ex, "DramaTrackFetcher: Failed to fetch section {SectionCode} for drama {PublicationCode} in language {LanguageCode}",
                    sectionCode, normalizedPublicationCode, normalizedLanguageCode);
                continue;
            }
        }

        logger.Information("DramaTrackFetcher: Completed processing. Successful sections: {SuccessfulCount}, Failed sections: {FailedCount}, Total tracks: {TrackCount}",
            successfulSections, failedSections, allTracks.Count);

        return allTracks;
    }

    private async Task<List<BiblePublicationTrack>> FetchTracksForSectionAsync(
        string sectionCode,
        string normalizedPublicationCode,
        string normalizedLanguageCode,
        BaseUrl baseUrl,
        CancellationToken cancellationToken)
    {
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={normalizedLanguageCode}";
        var sectionResponse = await httpClient.GetAsync(harvestLink, cancellationToken);
        
        if (!sectionResponse.IsSuccessStatusCode)
        {
            return new List<BiblePublicationTrack>();
        }

        var sectionJsonString = await sectionResponse.Content.ReadAsStringAsync(cancellationToken);
        using var sectionDoc = JsonDocument.Parse(sectionJsonString);
        var sectionRoot = sectionDoc.RootElement;

        if (sectionRoot.ValueKind != JsonValueKind.Object || !sectionRoot.TryGetProperty("files", out var sectionFilesElement))
        {
            return new List<BiblePublicationTrack>();
        }

        var tracks = trackParser.ParseTracksFromJson(
            sectionFilesElement, normalizedLanguageCode, sectionCode, baseUrl);
        
        return tracks;
    }
}
