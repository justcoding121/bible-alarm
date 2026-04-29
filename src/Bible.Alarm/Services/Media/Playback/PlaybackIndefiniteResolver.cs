#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Resolves next/previous play items for indefinite playback, including music injection logic.
/// </summary>
public sealed class PlaybackIndefiniteResolver
{
    private readonly IPlaylistService playlistService;
    private readonly ILogger logger;

    public PlaybackIndefiniteResolver(IPlaylistService playlistService, ILogger logger)
    {
        this.playlistService = playlistService;
        this.logger = logger;
    }

    private static bool HasMusicInjection(PlayItem? sessionMusicPlayItem, TrackMetadata? anchorBibleMetadata, TrackMetadata? preAnchorBibleMetadata) =>
        sessionMusicPlayItem != null && anchorBibleMetadata != null && preAnchorBibleMetadata != null;

    public static bool IsSameBibleTrack(TrackMetadata a, TrackMetadata b) =>
        a.PlayType == PlayType.Bible &&
        b.PlayType == PlayType.Bible &&
        a.LanguageCode == b.LanguageCode &&
        a.PublicationCode == b.PublicationCode &&
        a.SectionCode == b.SectionCode &&
        a.TrackCode == b.TrackCode;

    public async Task<PlayItem> ResolveNextPlayItemAsync(
        TrackMetadata currentMetadata,
        PlayItem? sessionMusicPlayItem,
        TrackMetadata? anchorBibleMetadata,
        TrackMetadata? preAnchorBibleMetadata,
        IFetchProgress? sectionProgress)
    {
        if (HasMusicInjection(sessionMusicPlayItem, anchorBibleMetadata, preAnchorBibleMetadata))
        {
            if (currentMetadata.PlayType == PlayType.Bible && IsSameBibleTrack(currentMetadata, preAnchorBibleMetadata!))
            {
                return sessionMusicPlayItem!;
            }

            if (currentMetadata.PlayType == PlayType.Music)
            {
                return await playlistService.GetNextPlayItemAsync(preAnchorBibleMetadata!, sectionProgress);
            }
        }

        return await playlistService.GetNextPlayItemAsync(currentMetadata, sectionProgress);
    }

    public async Task<PlayItem> ResolvePreviousPlayItemAsync(
        TrackMetadata currentMetadata,
        PlayItem? sessionMusicPlayItem,
        TrackMetadata? anchorBibleMetadata,
        TrackMetadata? preAnchorBibleMetadata,
        IFetchProgress? sectionProgress)
    {
        if (HasMusicInjection(sessionMusicPlayItem, anchorBibleMetadata, preAnchorBibleMetadata))
        {
            if (currentMetadata.PlayType == PlayType.Bible && IsSameBibleTrack(currentMetadata, anchorBibleMetadata!))
            {
                return sessionMusicPlayItem!;
            }

            if (currentMetadata.PlayType == PlayType.Music)
            {
                return await playlistService.GetPreviousPlayItemAsync(anchorBibleMetadata!, sectionProgress);
            }
        }

        return await playlistService.GetPreviousPlayItemAsync(currentMetadata, sectionProgress);
    }
}
