#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Builds PlayItem from Bible track navigation result.
/// </summary>
public sealed class PlaylistBiblePlayItemBuilder
{
    private readonly IUrlConstructionService urlConstructionService;
    private readonly IMediaUrlRefreshService urlRefreshService;

    public PlaylistBiblePlayItemBuilder(
        IUrlConstructionService urlConstructionService,
        IMediaUrlRefreshService urlRefreshService)
    {
        this.urlConstructionService = urlConstructionService ?? throw new ArgumentNullException(nameof(urlConstructionService));
        this.urlRefreshService = urlRefreshService ?? throw new ArgumentNullException(nameof(urlRefreshService));
    }

    public async Task<PlayItem> BuildPlayItemAsync(
        long scheduleId,
        string languageCode,
        string publicationCode,
        string? sectionCode,
        string trackCode)
    {
        var metadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            LanguageCode = languageCode,
            PublicationCode = publicationCode,
            SectionCode = sectionCode,
            TrackCode = trackCode,
            IsLastTrack = false
        };

        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(metadata);

        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            publicationCode,
            languageCode,
            sectionCode,
            trackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={publicationCode}, lang={languageCode}, section={sectionCode ?? "(none)"}, track={trackCode}. Only cataloged tracks can be played.");
        }
        metadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(metadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException("Failed to refresh URL for play item");
        }

        return new PlayItem(metadata, url);
    }
}
