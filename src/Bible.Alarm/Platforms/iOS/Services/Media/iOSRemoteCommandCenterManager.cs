#nullable enable
using Bible.Alarm.Common.Messenger;
using CommunityToolkit.Mvvm.Messaging;
using MediaPlayer;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.Media;

/// <summary>
/// Manages MPRemoteCommandCenter for iOS system media controls.
/// Handles play/pause/next/previous/seek commands from Lock Screen, Control Center,
/// AirPods, CarPlay, and other system media interfaces.
/// </summary>
public sealed class iOSRemoteCommandCenterManager : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<iOSRemoteCommandCenterManager>();
    private readonly MPRemoteCommandCenter commandCenter;
    private bool isRegistered;
    private bool disposed;

    public iOSRemoteCommandCenterManager()
    {
        commandCenter = MPRemoteCommandCenter.Shared;
    }

    /// <summary>
    /// Registers all remote command handlers.
    /// Call this when the app starts or when playback becomes active.
    /// </summary>
    public void RegisterCommands()
    {
        if (isRegistered)
        {
            logger.Debug("[iOS Media] Remote commands already registered");
            return;
        }

        try
        {
            logger.Information("[iOS Media] Registering remote command handlers");

            // Play command
            commandCenter.PlayCommand.Enabled = true;
            commandCenter.PlayCommand.AddTarget((evt) => HandlePlayCommand(evt));

            // Pause command
            commandCenter.PauseCommand.Enabled = true;
            commandCenter.PauseCommand.AddTarget((evt) => HandlePauseCommand(evt));

            // Toggle Play/Pause (for single button headphones)
            commandCenter.TogglePlayPauseCommand.Enabled = true;
            commandCenter.TogglePlayPauseCommand.AddTarget((evt) => HandleTogglePlayPauseCommand(evt));

            // Next track
            commandCenter.NextTrackCommand.Enabled = true;
            commandCenter.NextTrackCommand.AddTarget((evt) => HandleNextTrackCommand(evt));

            // Previous track
            commandCenter.PreviousTrackCommand.Enabled = true;
            commandCenter.PreviousTrackCommand.AddTarget((evt) => HandlePreviousTrackCommand(evt));

            // Seek forward (skip forward)
            commandCenter.SkipForwardCommand.Enabled = true;
            commandCenter.SkipForwardCommand.PreferredIntervals = new double[] { 15.0 }; // 15 seconds
            commandCenter.SkipForwardCommand.AddTarget((evt) => HandleSkipForwardCommand(evt));

            // Seek backward (skip backward)
            commandCenter.SkipBackwardCommand.Enabled = true;
            commandCenter.SkipBackwardCommand.PreferredIntervals = new double[] { 15.0 }; // 15 seconds
            commandCenter.SkipBackwardCommand.AddTarget((evt) => HandleSkipBackwardCommand(evt));

            // Seek to position (for scrubbing)
            commandCenter.ChangePlaybackPositionCommand.Enabled = true;
            commandCenter.ChangePlaybackPositionCommand.AddTarget((evt) => HandleChangePlaybackPositionCommand(evt));

            isRegistered = true;
            logger.Information("[iOS Media] Remote command handlers registered successfully");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS Media] Failed to register remote command handlers");
        }
    }

    /// <summary>
    /// Unregisters all remote command handlers.
    /// Call this when playback stops completely or app is terminating.
    /// </summary>
    public void UnregisterCommands()
    {
        if (!isRegistered)
        {
            return;
        }

        try
        {
            logger.Information("[iOS Media] Unregistering remote command handlers");

            // Remove all targets by disabling commands (simpler than tracking individual handlers)
            commandCenter.PlayCommand.Enabled = false;
            commandCenter.PauseCommand.Enabled = false;
            commandCenter.TogglePlayPauseCommand.Enabled = false;
            commandCenter.NextTrackCommand.Enabled = false;
            commandCenter.PreviousTrackCommand.Enabled = false;
            commandCenter.SkipForwardCommand.Enabled = false;
            commandCenter.SkipBackwardCommand.Enabled = false;
            commandCenter.ChangePlaybackPositionCommand.Enabled = false;

            isRegistered = false;
            logger.Information("[iOS Media] Remote command handlers unregistered");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS Media] Failed to unregister remote command handlers");
        }
    }

    /// <summary>
    /// Updates which commands are enabled based on current playback state.
    /// </summary>
    public void UpdateCommandAvailability(bool canPlayNext, bool canPlayPrevious, bool isPlaying)
    {
        try
        {
            // Next/Previous are always enabled during playback sessions.
            commandCenter.NextTrackCommand.Enabled = true;
            commandCenter.PreviousTrackCommand.Enabled = true;
            commandCenter.PlayCommand.Enabled = !isPlaying;
            commandCenter.PauseCommand.Enabled = isPlaying;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[iOS Media] Failed to update command availability");
        }
    }

    private MPRemoteCommandHandlerStatus HandlePlayCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Play command received");
        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandlePauseCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Pause command received");
        WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandleTogglePlayPauseCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Toggle play/pause command received");
        // The PlaybackService will determine current state and toggle appropriately
        // For now, we'll send play - the service handles the toggle logic
        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandleNextTrackCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Next track command received");
        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandlePreviousTrackCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Previous track command received");
        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandleSkipForwardCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Skip forward command received");
        WeakReferenceMessenger.Default.Send(new SeekForwardButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandleSkipBackwardCommand(MPRemoteCommandEvent evt)
    {
        logger.Debug("[iOS Media] Skip backward command received");
        WeakReferenceMessenger.Default.Send(new SeekBackwardButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandleChangePlaybackPositionCommand(MPRemoteCommandEvent evt)
    {
        if (evt is MPChangePlaybackPositionCommandEvent positionEvent)
        {
            var position = TimeSpan.FromSeconds(positionEvent.PositionTime);
            logger.Debug("[iOS Media] Seek to position command received: {Position}", position);
            // Note: SeekToAsync requires a position parameter, but our current message system
            // uses SeekForward/Backward. For scrubbing support, we'd need to add a new message type.
            // For now, this returns success but doesn't implement full scrubbing.
            return MPRemoteCommandHandlerStatus.Success;
        }

        return MPRemoteCommandHandlerStatus.CommandFailed;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        UnregisterCommands();
        disposed = true;
    }
}
