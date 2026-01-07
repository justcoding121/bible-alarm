#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.Windows.Effects;

/// <summary>
/// Fluxor effect that shows rich toast notifications with metadata during playback on Windows.
/// Shows artwork, title, subtitle (artist), and album information when track metadata changes.
/// </summary>
public class WindowsMediaToastEffect(
    WindowsNotificationService notificationService,
    IState<PlaybackState> playbackState) : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<WindowsMediaToastEffect>();

    [EffectMethod]
    public Task HandlePlaybackMetadataChanged(PlaybackMetadataChangedAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;
            
            // Only show toast if playback is active (playing or paused)
            if (currentState.Status != PlayStatus.Playing && currentState.Status != PlayStatus.Paused)
            {
                logger.Debug("Skipping media toast - playback not active (Status: {Status})", currentState.Status);
                // Dismiss toast if playback is not active
                notificationService.DismissMediaToast();
                return Task.CompletedTask;
            }

            // Only show if we have meaningful metadata
            if (string.IsNullOrWhiteSpace(action.Title) && string.IsNullOrWhiteSpace(action.Artist))
            {
                logger.Debug("Skipping media toast - no metadata available");
                return Task.CompletedTask;
            }

            logger.Debug("Showing media toast: Title={Title}, Artist={Artist}, ArtworkUrl={ArtworkUrl}",
                action.Title, action.Artist, action.ArtworkUrl);

            // Show toast notification with artwork, title, and subtitle (NO controls)
            // Clicking the toast will activate the existing app instance
            notificationService.ShowMediaToast(
                title: action.Title ?? "Now Playing",
                subtitle: action.Artist,
                body: action.Album,
                artworkUrl: action.ArtworkUrl,
                canPlayNext: false, // Not used - no buttons
                canPlayPrevious: false, // Not used - no buttons
                isPlaying: false); // Not used - no buttons
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing media toast notification");
        }

        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task HandlePlaybackStopped(PlaybackStoppedAction action, IDispatcher dispatcher)
    {
        try
        {
            logger.Debug("Playback stopped - dismissing media toast");
            notificationService.DismissMediaToast();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error dismissing media toast on playback stop");
        }

        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task HandlePlaybackStatusChanged(PlaybackStatusChangedAction action, IDispatcher dispatcher)
    {
        try
        {
            var currentState = playbackState.Value;
            
            // If playback is stopped/ended, dismiss the toast
            if (action.Status == PlayStatus.Stopped || action.Status == PlayStatus.Ended)
            {
                logger.Debug("Playback status changed to {Status} - dismissing media toast", action.Status);
                notificationService.DismissMediaToast();
                return Task.CompletedTask;
            }

            // Update toast when track changes (metadata will trigger HandlePlaybackMetadataChanged)
            // We don't need to update here since metadata change will handle it
            // This method is mainly for dismissing when playback stops
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating media toast on status change");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
