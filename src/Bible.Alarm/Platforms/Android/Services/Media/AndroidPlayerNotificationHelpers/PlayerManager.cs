#nullable enable
using System.Reflection;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.Media.AndroidPlayerNotificationHelpers;

/// <summary>
/// Handles ExoPlayer configuration and listener management for Android player notifications.
/// </summary>
public sealed class PlayerManager(ILogger logger)
{
    private ExoPlayerListener? exoPlayerListener;
    private IExoPlayer? currentPlayer;

    /// <summary>
    /// Gets the underlying ExoPlayer instance from MediaElement.
    /// </summary>
    public IExoPlayer? GetExoPlayer(MediaElement mediaElement)
    {
        try
        {
            // Access MediaElement's handler
            var handler = mediaElement.Handler as MediaElementHandler;
            if (handler == null)
            {
                logger.Debug("MediaElement handler is null or not MediaElementHandler");
                return null;
            }

            // Access MediaManager property directly (now public)
            var mediaManager = handler.MediaManager;
            if (mediaManager == null)
            {
                logger.Debug("MediaManager is null");
                return null;
            }

            // Access Player property directly (now public)
            var player = mediaManager.Player as IExoPlayer;
            if (player == null)
            {
                logger.Debug("Player is null or not IExoPlayer");
                return null;
            }

            return player;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to get ExoPlayer");
            return null;
        }
    }

    /// <summary>
    /// Configures the player with the provided media sources and sets up the listener.
    /// Queue layout is always [previous_dummy, current, next_dummy], so current is at index 1.
    /// </summary>
    public void ConfigurePlayerWithSources(IExoPlayer player, List<IMediaSource> sources)
    {
        // Clear existing timeline before replacing. When the previous track ended naturally,
        // ExoPlayer is in STATE_ENDED. Replacing the timeline directly in that state causes
        // MediaSessionImpl.PlayerInfo.Builder.build() to throw IllegalStateException
        // (copyWithTimelineAndSessionPositionInfo fails due to invalid session position).
        // Clearing first transitions to IDLE, avoiding the invalid state.
        player.ClearMediaItems();

        // Set media sources directly on the player
        player.SetMediaSources([.. sources]);

        // CRITICAL: Call Prepare() AFTER setting the source to trigger TimelineChanged event
        // This is what makes MediaSessionConnector see HasNextMediaItem and HasPreviousMediaItem = true
        player.Prepare();

        // Current item is always at index 1 (previous dummy is always at index 0).
        player.SeekTo(1, 0);

        // Set up the ExoPlayer listener
        SetupExoPlayerListener(player);
    }

    /// <summary>
    /// Verifies player capabilities and logs relevant information.
    /// </summary>
    public void VerifyPlayerCapabilities(IExoPlayer player)
    {
        if (player.HasNextMediaItem)
        {
            logger.Debug("Player HasNextMediaItem: {HasNext}", player.HasNextMediaItem);
        }
        else
        {
            logger.Debug("Player HasNextMediaItem is still false after setting media sources");
        }

        // Check for HasPreviousMediaItem property via reflection
        var playerType = player.GetType();
        var hasPreviousProperty = playerType.GetProperty("HasPreviousMediaItem", BindingFlags.Public | BindingFlags.Instance);
        if (hasPreviousProperty != null)
        {
            var hasPrevious = hasPreviousProperty.GetValue(player);
            logger.Debug("Player HasPreviousMediaItem: {HasPrevious}", hasPrevious);
        }
    }

    /// <summary>
    /// Sets up the ExoPlayer listener to intercept Next/Previous button presses.
    /// </summary>
    private void SetupExoPlayerListener(IExoPlayer player)
    {
        try
        {
            CleanupExistingListener();
            CreateAndAttachNewListener(player);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set up ExoPlayer listener");
        }
    }

    /// <summary>
    /// Cleans up any existing listener.
    /// </summary>
    private void CleanupExistingListener()
    {
        // Always remove the old listener first, regardless of whether it's the same player or different
        // This prevents duplicate listeners when SetSourceWithDummyQueue is called multiple times
        if (exoPlayerListener != null && currentPlayer != null)
        {
            RemoveExoPlayerListener();
        }
    }

    /// <summary>
    /// Creates and attaches a new listener to the player.
    /// </summary>
    private void CreateAndAttachNewListener(IExoPlayer player)
    {
        // Create and add new listener
        currentPlayer = player;
        exoPlayerListener = new ExoPlayerListener(logger);

        // AddListener is a direct method on IExoPlayer - no reflection needed
        player.AddListener(exoPlayerListener);
        logger.Information("ExoPlayer listener attached — OnMediaItemTransition will fire on Next/Previous press");
    }

    /// <summary>
    /// Removes the ExoPlayer listener and cleans up references.
    /// </summary>
    public void RemoveExoPlayerListener()
    {
        try
        {
            if (exoPlayerListener != null && currentPlayer != null)
            {
                try
                {
                    // RemoveListener is a direct method on IExoPlayer - no reflection needed
                    currentPlayer.RemoveListener(exoPlayerListener);
                    logger.Debug("Removed ExoPlayer listener from player");
                }
                catch (ObjectDisposedException ex)
                {
                    // Player is already disposed - this is expected when handler is disconnected
                    logger.Debug(ex, "ExoPlayer is already disposed, skipping RemoveListener call");
                }
                catch (Exception ex)
                {
                    // Other exceptions during removal - log but continue
                    logger.Debug(ex, "Error removing listener from player (may be disposed)");
                }

                // Dispose the listener
                try
                {
                    exoPlayerListener.Dispose();
                    logger.Debug("Disposed ExoPlayer listener");
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Error disposing listener");
                }

                exoPlayerListener = null;
            }

            currentPlayer = null;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error in RemoveExoPlayerListener");
        }
    }

    /// <summary>
    /// Sends NextButtonPressedMessage via Messenger.
    /// </summary>
    internal static void OnNextButtonPressed() =>
        // Just send the message - don't try to manipulate ExoPlayer here
        // PlaybackService will handle stopping and preparing the next track
        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());

    /// <summary>
    /// Sends PreviousButtonPressedMessage via Messenger.
    /// </summary>
    internal static void OnPreviousButtonPressed() =>
        // Just send the message - don't try to manipulate ExoPlayer here
        // PlaybackService will handle stopping and preparing the previous track
        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());

    /// <summary>
    /// ExoPlayer listener that intercepts Next/Previous button presses from system controls.
    /// </summary>
    private class ExoPlayerListener(ILogger logger) : Object, IPlayerListener
    {
        private DateTime lastButtonPressTime = DateTime.MinValue;
        private string? lastMediaId;
        // Ignore duplicate presses within 500ms
        private const int DebounceMilliseconds = 500;

        /// <summary>
        /// Checks if the media transition should be processed or debounced.
        /// </summary>
        private bool ShouldProcessMediaTransition(string? mediaId, string transitionType)
        {
            // If mediaId is null, we can't debounce it, so process it
            if (mediaId == null)
            {
                return true;
            }

            var now = DateTime.UtcNow;

            // Debounce: ignore duplicate transitions within the debounce window
            if (lastMediaId == mediaId &&
                (now - lastButtonPressTime).TotalMilliseconds < DebounceMilliseconds)
            {
                logger.Debug("Ignoring duplicate {TransitionType} for {MediaId} (debounced)", transitionType, mediaId);
                return false;
            }

            lastButtonPressTime = now;
            lastMediaId = mediaId;
            return true;
        }

        public void OnMediaItemTransition(AndroidX.Media3.Common.MediaItem? mediaItem, int reason)
        {
            const int AutomaticTransitionReason = 1;
            const int MediaItemTransitionReasonManual = 2;

            if (mediaItem != null)
            {
                var mediaId = mediaItem.MediaId;

                if (reason == MediaItemTransitionReasonManual)
                {
                    if (!ShouldProcessMediaTransition(mediaId, "button press"))
                    {
                        return;
                    }

                    if (mediaId == "bible_alarm_next_dummy")
                    {
                        logger.Information("NEXT BUTTON PRESSED — BLOCKING DUMMY TRACK");
                        OnNextButtonPressed();
                    }
                    else if (mediaId == "bible_alarm_previous_dummy")
                    {
                        logger.Information("PREVIOUS BUTTON PRESSED — BLOCKING DUMMY TRACK");
                        OnPreviousButtonPressed();
                    }
                }
                else if (reason == AutomaticTransitionReason
                         && mediaId == "bible_alarm_next_dummy")
                {
                    // Do NOT call OnNextButtonPressed() here.
                    // The automatic transition to the silent dummy is an ExoPlayer queue artifact,
                    // not a user action. Calling PlayNextAsync would force IsIndefinitePlayback = true
                    // and ignore the NumberOfTracksToPlay limit.
                    // MediaElement's MediaEnded event fires after the silent dummy finishes
                    // (ExoPlayer STATE_ENDED) and HandleMediaEndedAsync properly respects
                    // the finite/indefinite playback mode.
                    logger.Information("AUTOMATIC TRANSITION to dummy next track — ignored (MediaEnded will handle track end)");
                }
            }
        }

        // Required interface methods - can be empty
        public void OnAudioAttributesChanged(AndroidX.Media3.Common.AudioAttributes? audioAttributes) { }
        public void OnAudioSessionIdChanged(int audioSessionId) { }
        public void OnAvailableCommandsChanged(PlayerCommands? player) { }
        public void OnCues(AndroidX.Media3.Common.Text.CueGroup? cues) { }
        public void OnDeviceInfoChanged(AndroidX.Media3.Common.DeviceInfo? deviceInfo) { }
        public void OnDeviceVolumeChanged(int volume, bool muted) { }
        public void OnEvents(IExoPlayer? player, PlayerEvents? playerEvents) { }
        public void OnIsLoadingChanged(bool isLoading) { }
        public void OnIsPlayingChanged(bool isPlaying) { }
        public void OnLoadingChanged(bool isLoading) { }
        public void OnMaxSeekToPreviousPositionChanged(long maxSeekToPreviousPositionMs) { }
        public void OnMediaMetadataChanged(AndroidX.Media3.Common.MediaMetadata? mediaMetadata) { }
        public void OnMetadata(AndroidX.Media3.Common.Metadata? metadata) { }
        public void OnPlaybackParametersChanged(PlaybackParameters? playbackParameters) { }
        public void OnPlaybackStateChanged(int playbackState) { }
        public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason) { }
        public void OnPlayWhenReadyChanged(bool playWhenReady, int reason) { }
        public void OnPlayerStateChanged(bool playWhenReady, int playbackState) { }
        public void OnPlayerError(PlaybackException? error) { }
        public void OnPlayerErrorChanged(PlaybackException? error) { }
        public void OnPlaylistMetadataChanged(AndroidX.Media3.Common.MediaMetadata? mediaMetadata) { }
        public void OnPositionDiscontinuity(PlayerPositionInfo? oldPosition, PlayerPositionInfo? newPosition, int reason) { }
        public void OnRepeatModeChanged(int repeatMode) { }
        public void OnRenderedFirstFrame() { }
        public void OnSeekBackIncrementChanged(long seekBackIncrementMs) { }
        public void OnSeekForwardIncrementChanged(long seekForwardIncrementMs) { }
        public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
        public void OnSkipSilenceEnabledChanged(bool skipSilenceEnabled) { }
        public void OnSurfaceSizeChanged(int width, int height) { }
        public void OnTimelineChanged(Timeline? timeline, int reason) { }
        public void OnTrackSelectionParametersChanged(TrackSelectionParameters? trackSelectionParameters) { }
        public void OnTracksChanged(Tracks? tracks) { }
        public void OnVolumeChanged(float volume) { }
    }
}
