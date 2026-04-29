#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;

internal sealed class DisplayMetadataServiceMusicHelper
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IVocalMusicService? vocalMusicService;

    public DisplayMetadataServiceMusicHelper(ILogger logger, IMediaService mediaService, IVocalMusicService? vocalMusicService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.vocalMusicService = vocalMusicService;
    }

    public async Task SetMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta, string uri, Func<Task> tryExtractFileMetadataAsync)
    {
        if (string.IsNullOrEmpty(trackMetadata.LanguageCode))
            await SetMelodyMusicMetadataAsync(trackMetadata, meta);
        else
            await SetVocalMusicMetadataAsync(trackMetadata, meta);
        await tryExtractFileMetadataAsync();
    }

    private async Task SetMelodyMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        SortedDictionary<int, MusicTrack> tracks;
        if (PublicationTypeHelper.HasSectionStructure(trackMetadata.PublicationCode) && !string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
            tracks = await mediaService.GetMelodyMusicTracksBySection(trackMetadata.PublicationCode, trackMetadata.DownloadCode);
        else
            tracks = await mediaService.GetMelodyMusicTracks(trackMetadata.PublicationCode);

        var trackCode = trackMetadata.TrackCode;
        if (!string.IsNullOrWhiteSpace(trackCode) &&
            Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, trackCode, out var melodyPair))
            meta.Title = NormalizeTitle(melodyPair.Track.Title);

        try
        {
            var releases = await mediaService.GetMelodyMusicReleases();
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var release) && !string.IsNullOrWhiteSpace(release?.Name))
                meta.Artist = $"{release.Name}{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}";
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToResolveMelodyReleaseNameForPublication, trackMetadata.PublicationCode);
        }

        if (PublicationTypeHelper.HasSectionStructure(trackMetadata.PublicationCode) && !string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
        {
            try
            {
                var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(trackMetadata.PublicationCode);
                if (sections.TryGetValue(trackMetadata.DownloadCode, out var section) && !string.IsNullOrWhiteSpace(section?.Name))
                    meta.Album = section.Name;
            }
            catch (Exception ex)
            {
                logger.Debug(ex, AppConstants.Logging.DisplayMetadataServiceDiagnosticsLog.FailedToResolveMelodyDiscNameForPublicationDisc, trackMetadata.PublicationCode, trackMetadata.DownloadCode);
            }
        }
    }

    private async Task SetVocalMusicMetadataAsync(TrackMetadata trackMetadata, MetaData meta)
    {
        if (vocalMusicService != null)
        {
            var release = await vocalMusicService.GetByLanguageAndCodeAsync(trackMetadata.LanguageCode, trackMetadata.PublicationCode);
            if (release != null)
                meta.Album = release.Name;
        }
        else
        {
            var releases = await mediaService.GetVocalMusicReleases(trackMetadata.LanguageCode);
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var vocalRelease))
                meta.Album = vocalRelease.Name;
        }

        var tracks = await mediaService.GetVocalMusicTracks(trackMetadata.LanguageCode, trackMetadata.PublicationCode);
        if (!string.IsNullOrWhiteSpace(trackMetadata.TrackCode) &&
            Bible.Alarm.Shared.Helpers.MusicTrackLookupHelper.TryGetByCode(tracks, trackMetadata.TrackCode, out var vocalPair))
            meta.Title = vocalPair.Track.Title;
    }

    private static string? NormalizeTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return null;
        return MediaTrackTitleHelper.DecodeHtmlTitle(rawTitle).Trim();
    }
}
