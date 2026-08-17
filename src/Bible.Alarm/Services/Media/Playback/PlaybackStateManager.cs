#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

public sealed class PlaybackStateManager
{
    private readonly ILogger logger;

    public List<AudioPlayerTrack>? Playlist { get; set; }
    public int CurrentTrackIndex { get; set; } = -1;
    public int? CurrentScheduleId { get; set; }
    public bool IsAlarm { get; set; }
    /// <summary>
    /// True when the schedule is configured to play indefinitely (NumberOfTracksToPlay == 0).
    /// Captured when a playback session starts.
    /// </summary>
    public bool IsIndefinitePlayback { get; set; }
    public TrackMetadata? AnchorBibleMetadata { get; set; }
    public TrackMetadata? PreAnchorBibleMetadata { get; set; }
    public PlayItem? SessionMusicPlayItem { get; set; }
    public HashSet<int> ManuallyVisitedTrackIndices { get; } = [];
    /// <summary>
    /// Tracks Bible tracks that have been played in this session (by unique track key).
    /// Used to prevent seeking to saved progress when returning to a previously played track.
    /// Key format: "{ScheduleId}:{LanguageCode}:{PublicationCode}:{SectionCode}:{TrackCode}"
    /// </summary>
    public HashSet<string> PlayedBibleTrackKeys { get; } = [];
    public CancellationTokenSource? PreparationCancellationTokenSource { get; set; }
    public bool IsPreparingTrack { get; set; }
    /// <summary>
    /// True when Next/Previous was pressed from system controls (notification) and PlayNextAsync/PlayPreviousAsync
    /// is pending. Prevents MediaEnded (from dummy track) from advancing; the manual handler performs the single advance.
    /// </summary>
    public bool ManualNavigationPending { get; set; }

    private int playbackEstablishedTrackIndex = -2;

    public void NotifyPlaybackEstablishedForTrack(int trackIndex)
    {
        playbackEstablishedTrackIndex = trackIndex;
    }

    public bool IsPlaybackEstablishedForTrack(int trackIndex)
    {
        return playbackEstablishedTrackIndex == trackIndex;
    }

    public PlaybackStateManager(ILogger logger)
    {
        this.logger = logger;
    }

    public void Reset()
    {
        var playedTracksCount = PlayedBibleTrackKeys.Count;
        CurrentScheduleId = null;
        Playlist = null;
        CurrentTrackIndex = -1;
        IsAlarm = false;
        IsIndefinitePlayback = false;
        AnchorBibleMetadata = null;
        PreAnchorBibleMetadata = null;
        SessionMusicPlayItem = null;
        ManuallyVisitedTrackIndices.Clear();
        PlayedBibleTrackKeys.Clear();
        ManualNavigationPending = false;
        IsPreparingTrack = false;
        playbackEstablishedTrackIndex = -2;
        logger.Debug(AppConstants.Logging.PlaybackStateManagerDiagnosticsLog.ResetClearedPlayedBibleTrackKeys, playedTracksCount);

        // Dispose cancellation token source
        try
        {
            PreparationCancellationTokenSource?.Dispose();
            PreparationCancellationTokenSource = null;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.PlaybackStateManagerDiagnosticsLog.ErrorDisposingPreparationCancellationTokenSource);
        }
    }

    public bool IsPreparingOrPlaying(IAudioPlayer audioPlayer)
    {
        // If we're actively preparing a track, always return true
        // This prevents race conditions where state hasn't transitioned yet
        if (IsPreparingTrack)
        {
            return true;
        }

        var isActuallyPlaying = audioPlayer.IsActuallyPlayingOrPaused;
        var status = audioPlayer.Status;
        return isActuallyPlaying ||
               status == PlayStatus.Loading ||
               status == PlayStatus.Playing ||
               status == PlayStatus.Paused;
    }
}

