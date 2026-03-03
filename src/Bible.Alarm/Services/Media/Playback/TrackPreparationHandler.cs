#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles track preparation and playback logic.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class TrackPreparationHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly IPlaylistService playlistService;
    private readonly ILogger logger;

    public TrackPreparationHandler(
        IAudioPlayer audioPlayer,
        IPlaylistService playlistService,
        ILogger logger)
    {
        this.audioPlayer = audioPlayer;
        this.playlistService = playlistService;
        this.logger = logger;
    }

    public async Task WaitForMediaReadyAsync()
    {
        // On iOS, MediaElement may need a moment after PrepareAsync before it can play.
        // When streaming from CDN on slow networks (e.g. 4G), buffering can take several seconds.
        // Wait for the MediaElement to transition from "Opening" to a ready state (Paused, Playing, or Buffering).
        var maxWaitTime = TimeSpan.FromSeconds(10);
        var checkInterval = TimeSpan.FromMilliseconds(50);
        var elapsed = TimeSpan.Zero;

        while (elapsed < maxWaitTime)
        {
            // Check if MediaElement is in a ready state
            if (audioPlayer.IsActuallyPlayingOrPaused ||
                audioPlayer.Status == PlayStatus.Loading ||
                audioPlayer.Status == PlayStatus.Playing ||
                audioPlayer.Status == PlayStatus.Paused)
            {
                logger.Debug("Media ready check completed - MediaElement is in ready state, proceeding to play");
                return;
            }

            await Task.Delay(checkInterval);
            elapsed = elapsed.Add(checkInterval);
        }

        // If we've waited the max time, proceed anyway - the state might still transition
        logger.Debug("Media ready check completed after timeout - proceeding to play anyway");
    }

    public async Task<bool> ShouldResumeFromLastPositionAsync(int? currentScheduleId)
    {
        if (!currentScheduleId.HasValue)
        {
            return false;
        }

        try
        {
            return await playlistService.ShouldResumeFromLastPositionAsync(currentScheduleId.Value);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking if should resume from last position");
            return false;
        }
    }
}

