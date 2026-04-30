#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Services.CarPlay;
using Bible.Alarm.Platforms.iOS.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Foundation;
using MediaPlayer;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.Media;

/// <summary>
/// Manages MPRemoteCommandCenter for iOS system media controls.
/// Handles play/pause/next/previous/seek commands from Lock Screen, Control Center,
/// AirPods, CarPlay, and other system media interfaces.
/// </summary>
public sealed class iOSRemoteCommandCenterManager : IiOSRemoteCommandCenterManager
{
    private static readonly ILogger logger = Log.ForContext<iOSRemoteCommandCenterManager>();
    private readonly MPRemoteCommandCenter commandCenter;
    private readonly IState<PlaybackState> playbackState;
    private bool isRegistered;
    private bool disposed;

    // Tokens returned by AddTarget — required to actually remove the handler via RemoveTarget.
    // Simply setting Enabled = false only hides the lock-screen affordance; the lambda keeps firing.
    private NSObject? playToken;
    private NSObject? pauseToken;
    private NSObject? toggleToken;
    private NSObject? nextToken;
    private NSObject? previousToken;
    private NSObject? skipForwardToken;
    private NSObject? skipBackwardToken;
    private NSObject? changePositionToken;

    public iOSRemoteCommandCenterManager(IState<PlaybackState> playbackState)
    {
        commandCenter = MPRemoteCommandCenter.Shared;
        this.playbackState = playbackState;
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
            playToken = commandCenter.PlayCommand.AddTarget((_) => HandlePlayCommand());

            // Pause command
            commandCenter.PauseCommand.Enabled = true;
            pauseToken = commandCenter.PauseCommand.AddTarget((_) => HandlePauseCommand());

            // Toggle Play/Pause (for single button headphones)
            commandCenter.TogglePlayPauseCommand.Enabled = true;
            toggleToken = commandCenter.TogglePlayPauseCommand.AddTarget((evt) => HandleTogglePlayPauseCommand(evt));

            // Next track
            commandCenter.NextTrackCommand.Enabled = true;
            nextToken = commandCenter.NextTrackCommand.AddTarget((_) => HandleNextTrackCommand());

            // Previous track
            commandCenter.PreviousTrackCommand.Enabled = true;
            previousToken = commandCenter.PreviousTrackCommand.AddTarget((_) => HandlePreviousTrackCommand());

            // Seek forward (skip forward)
            commandCenter.SkipForwardCommand.Enabled = true;
            // 15 seconds
            commandCenter.SkipForwardCommand.PreferredIntervals = new double[] { 15.0 };
            skipForwardToken = commandCenter.SkipForwardCommand.AddTarget((_) => HandleSkipForwardCommand());

            // Seek backward (skip backward)
            commandCenter.SkipBackwardCommand.Enabled = true;
            // 15 seconds
            commandCenter.SkipBackwardCommand.PreferredIntervals = new double[] { 15.0 };
            skipBackwardToken = commandCenter.SkipBackwardCommand.AddTarget((_) => HandleSkipBackwardCommand());

            // Seek to position (for scrubbing)
            commandCenter.ChangePlaybackPositionCommand.Enabled = true;
            changePositionToken = commandCenter.ChangePlaybackPositionCommand.AddTarget((evt) => HandleChangePlaybackPositionCommand(evt));

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

            // RemoveTarget fully removes the handler registered by AddTarget so the lambda
            // is not called in future sessions.  Enabled = false only hides the lock-screen
            // affordance — without RemoveTarget the lambda keeps accumulating across sessions.
            RemoveToken(commandCenter.PlayCommand, ref playToken);
            RemoveToken(commandCenter.PauseCommand, ref pauseToken);
            RemoveToken(commandCenter.TogglePlayPauseCommand, ref toggleToken);
            RemoveToken(commandCenter.NextTrackCommand, ref nextToken);
            RemoveToken(commandCenter.PreviousTrackCommand, ref previousToken);
            RemoveToken(commandCenter.SkipForwardCommand, ref skipForwardToken);
            RemoveToken(commandCenter.SkipBackwardCommand, ref skipBackwardToken);
            RemoveToken(commandCenter.ChangePlaybackPositionCommand, ref changePositionToken);

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

    private static void RemoveToken(MPRemoteCommand command, ref NSObject? token)
    {
        if (token == null)
        {
            return;
        }

        try
        {
            command.RemoveTarget(token);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[iOS Media] RemoveTarget failed for command {Command}", command.GetType().Name);
        }
        finally
        {
            token = null;
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

    private MPRemoteCommandHandlerStatus HandlePlayCommand()
    {
        if (ShouldSuppressCarPlayConnectResume())
        {
            logger.Information(
                "[iOS Media] Play command suppressed — CarPlay just connected while not playing (auto-play prevention)");
            return MPRemoteCommandHandlerStatus.Success;
        }

        logger.Debug("[iOS Media] Play command received");
        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private static MPRemoteCommandHandlerStatus HandlePauseCommand()
    {
        logger.Debug("[iOS Media] Pause command received");
        WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus HandleTogglePlayPauseCommand(MPRemoteCommandEvent evt)
    {
        _ = evt;
        if (ShouldSuppressCarPlayConnectResume())
        {
            logger.Information(
                "[iOS Media] Toggle play/pause suppressed — CarPlay just connected while not playing (auto-play prevention)");
            return MPRemoteCommandHandlerStatus.Success;
        }

        logger.Debug("[iOS Media] Toggle play/pause command received");
        WeakReferenceMessenger.Default.Send(new TogglePlayPauseMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    // CarPlay can send Play/Toggle on attach. While paused, IsPreparingOrPlaying is still true, so we key off Status != Playing.
    private bool ShouldSuppressCarPlayConnectResume()
    {
        return CarPlaySceneDelegate.IsRecentlyConnected
            && playbackState.Value.Status != PlayStatus.Playing;
    }

    private static MPRemoteCommandHandlerStatus HandleNextTrackCommand()
    {
        logger.Debug("[iOS Media] Next track command received");
        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private static MPRemoteCommandHandlerStatus HandlePreviousTrackCommand()
    {
        logger.Debug("[iOS Media] Previous track command received");
        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private static MPRemoteCommandHandlerStatus HandleSkipForwardCommand()
    {
        logger.Debug("[iOS Media] Skip forward command received");
        WeakReferenceMessenger.Default.Send(new SeekForwardButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private static MPRemoteCommandHandlerStatus HandleSkipBackwardCommand()
    {
        logger.Debug("[iOS Media] Skip backward command received");
        WeakReferenceMessenger.Default.Send(new SeekBackwardButtonPressedMessage());
        return MPRemoteCommandHandlerStatus.Success;
    }

    private static MPRemoteCommandHandlerStatus HandleChangePlaybackPositionCommand(MPRemoteCommandEvent evt)
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
