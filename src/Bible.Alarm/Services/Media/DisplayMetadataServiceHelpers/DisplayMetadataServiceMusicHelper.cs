#nullable enable

using System.Net;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
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

        var trackCode = trackMetadata.OriginalTrackCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? trackMetadata.TrackCode;
        if (!string.IsNullOrWhiteSpace(trackCode) &&
            int.TryParse(trackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) &&
            tracks.TryGetValue(trackNum, out var melodyTrack))
            meta.Title = NormalizeTitle(melodyTrack.Title);

        try
        {
            var releases = await mediaService.GetMelodyMusicReleases();
            if (releases.TryGetValue(trackMetadata.PublicationCode, out var release) && !string.IsNullOrWhiteSpace(release?.Name))
                meta.Artist = $"{release.Name} (jw.org)";
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to resolve melody release name for {PublicationCode}", trackMetadata.PublicationCode);
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
                logger.Debug(ex, "Failed to resolve melody disc name for {PublicationCode}/{DiscCode}", trackMetadata.PublicationCode, trackMetadata.DownloadCode);
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
            int.TryParse(trackMetadata.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var trackNum) &&
            tracks.TryGetValue(trackNum, out var vocalTrack))
            meta.Title = vocalTrack.Title;
    }

    private static string? NormalizeTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return null;
        return WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ').Trim();
    }
}
