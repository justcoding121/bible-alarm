#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Media;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.Windows.Effects;

/// <summary>
/// Fluxor effect that initializes Windows SMTC service and updates button states based on navigation changes.
/// </summary>
public class WindowsSmtcEffect(
    WindowsSmtcService smtcService,
    IState<PlaybackState> playbackState) : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<WindowsSmtcEffect>();

    /// <summary>
    /// Initializes SMTC service when playback starts.
    /// </summary>
    [EffectMethod]
    public async Task HandlePlaybackStarted(PlaybackStartedAction action, IDispatcher dispatcher)
    {
        try
        {
            logger.Debug("Playback started - initializing SMTC service");
            await smtcService.InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing SMTC service on playback start");
        }
    }

    /// <summary>
    /// Updates SMTC button states when navigation capabilities change.
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackNavigationChanged(PlaybackNavigationChangedAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;

            // Update SMTC button states based on navigation capabilities
            // Enable buttons when playback is active (playing or paused)
            var canPlayNext = (currentState.Status == PlayStatus.Playing || currentState.Status == PlayStatus.Paused)
                ? action.CanPlayNext
                : false;

            // Previous button is always available when playing or paused (can restart current track)
            var canPlayPrevious = (currentState.Status == PlayStatus.Playing || currentState.Status == PlayStatus.Paused);

            smtcService.UpdateButtonStates(canPlayNext, canPlayPrevious);

            logger.Debug("SMTC button states updated: CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                canPlayNext, canPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating SMTC button states");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates SMTC button states when playback status changes.
    /// This ensures buttons are enabled/disabled correctly when playback starts/stops.
    /// </summary>
    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;

            // Update button states when status changes
            // Enable buttons when playing or paused
            var canPlayNext = (action.Status == PlayStatus.Playing || action.Status == PlayStatus.Paused)
                ? currentState.CanPlayNext
                : false;

            // Previous button is always available when playing or paused
            var canPlayPrevious = (action.Status == PlayStatus.Playing || action.Status == PlayStatus.Paused);

            smtcService.UpdateButtonStates(canPlayNext, canPlayPrevious);

            logger.Debug("SMTC button states updated on status change: Status={Status}, CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}",
                action.Status, canPlayNext, canPlayPrevious);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating SMTC button states on status change");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // SMTC service will be disposed by DI container
    }
}
