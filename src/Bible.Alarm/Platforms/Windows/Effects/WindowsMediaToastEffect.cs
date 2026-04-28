#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.Windows.Effects;

/// <summary>
/// Fluxor effect that shows rich toast notifications with metadata during playback on Windows.
/// Only shows when the app is not the active foreground window (minimized or behind other windows).
/// </summary>
public class WindowsMediaToastEffect(
    IWindowsNotificationService notificationService,
    IState<PlaybackState> playbackState) : IDisposable
{
    private static readonly ILogger logger = Log.ForContext<WindowsMediaToastEffect>();
    private bool isAppInForeground = true;
    private bool isLifecycleSubscribed;

    private void EnsureLifecycleSubscribed()
    {
        if (isLifecycleSubscribed) return;
        isLifecycleSubscribed = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var window = Application.Current?.Windows?.FirstOrDefault();
            if (window == null)
            {
                isLifecycleSubscribed = false;
                return;
            }

            window.Activated += OnWindowActivated;
            window.Deactivated += OnWindowDeactivated;
            logger.Debug("Subscribed to window lifecycle events for media toast foreground suppression");
        });
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        isAppInForeground = true;
        notificationService.DismissMediaToast();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        isAppInForeground = false;
    }

    [EffectMethod]
    public Task HandlePlaybackMetadataChanged(PlaybackMetadataChangedAction action, IDispatcher dispatcher)
    {
        try
        {
            EnsureLifecycleSubscribed();

            var currentState = playbackState.Value;

            if (currentState.Status != PlayStatus.Playing && currentState.Status != PlayStatus.Paused)
            {
                logger.Debug("Skipping media toast - playback not active (Status: {Status})", currentState.Status);
                notificationService.DismissMediaToast();
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(action.Title) && string.IsNullOrWhiteSpace(action.Artist))
            {
                logger.Debug("Skipping media toast - no metadata available");
                return Task.CompletedTask;
            }

            if (isAppInForeground)
            {
                logger.Debug("Skipping media toast - app is in foreground");
                return Task.CompletedTask;
            }

            logger.Debug("Showing media toast: Title={Title}, Artist={Artist}, ArtworkUrl={ArtworkUrl}",
                action.Title, action.Artist, action.ArtworkUrl);

            notificationService.ShowMediaToast(
                title: action.Title ?? "Now Playing",
                subtitle: action.Artist,
                body: action.Album,
                artworkUrl: action.ArtworkUrl,
                canPlayNext: false,
                canPlayPrevious: false,
                isPlaying: false);
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
        if (!isLifecycleSubscribed) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var window = Application.Current?.Windows?.FirstOrDefault();
            if (window == null) return;

            window.Activated -= OnWindowActivated;
            window.Deactivated -= OnWindowDeactivated;
        });
    }
}
