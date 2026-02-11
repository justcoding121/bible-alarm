#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles appending/prepending tracks for indefinite playback.
/// Tracks are added with an empty URI; the URI is resolved on-demand when playback reaches them.
/// </summary>
public sealed class PlaybackPlaylistExtender
{
    private readonly PlaybackIndefiniteResolver indefiniteResolver;
    private readonly ILogger logger;

    public PlaybackPlaylistExtender(
        PlaybackIndefiniteResolver indefiniteResolver,
        ILogger logger)
    {
        this.indefiniteResolver = indefiniteResolver;
        this.logger = logger;
    }

    public async Task<bool> TryAppendNextTrackAsync(
        List<AudioPlayerTrack> playlist,
        int currentIndex,
        PlayItem? sessionMusicPlayItem,
        TrackMetadata? anchorBibleMetadata,
        TrackMetadata? preAnchorBibleMetadata,
        IFetchProgress? sectionProgress,
        CancellationToken cancellationToken)
    {
        if (playlist == null || playlist.Count == 0 || currentIndex < 0 || currentIndex >= playlist.Count)
        {
            return false;
        }

        try
        {
            var currentMetadata = playlist[currentIndex].PlayItem.Metadata;
            var nextPlayItem = await indefiniteResolver.ResolveNextPlayItemAsync(
                currentMetadata,
                sessionMusicPlayItem,
                anchorBibleMetadata,
                preAnchorBibleMetadata,
                sectionProgress);

            // Add with empty URI; will be resolved on-demand when this track starts playing.
            var nextTrack = new AudioPlayerTrack
            {
                PlayItem = nextPlayItem,
                Uri = string.Empty
            };
            playlist.Add(nextTrack);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to append next track for indefinite playback");
            return false;
        }
    }

    public async Task<bool> TryPrependPreviousTrackAsync(
        List<AudioPlayerTrack> playlist,
        int currentIndex,
        int? currentScheduleId,
        PlayItem? sessionMusicPlayItem,
        TrackMetadata? anchorBibleMetadata,
        TrackMetadata? preAnchorBibleMetadata,
        IFetchProgress? sectionProgress)
    {
        if (playlist == null || playlist.Count == 0 || currentIndex < 0 || currentIndex >= playlist.Count)
        {
            return false;
        }

        try
        {
            var currentMetadata = playlist[currentIndex].PlayItem.Metadata;
            var prevPlayItem = await indefiniteResolver.ResolvePreviousPlayItemAsync(
                currentMetadata,
                sessionMusicPlayItem,
                anchorBibleMetadata,
                preAnchorBibleMetadata,
                sectionProgress);

            // Add with empty URI; will be resolved on-demand when this track starts playing.
            var prevTrack = new AudioPlayerTrack
            {
                PlayItem = prevPlayItem,
                Uri = string.Empty
            };
            playlist.Insert(0, prevTrack);
            return true;
        }
        catch (Exception ex)
        {
            logger.Warning(ex,
                "Failed to prepend previous track: ScheduleId={ScheduleId}, LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}",
                currentScheduleId,
                playlist[currentIndex].PlayItem.Metadata.LanguageCode,
                playlist[currentIndex].PlayItem.Metadata.PublicationCode,
                playlist[currentIndex].PlayItem.Metadata.SectionCode,
                playlist[currentIndex].PlayItem.Metadata.TrackCode);
            return false;
        }
    }
}
