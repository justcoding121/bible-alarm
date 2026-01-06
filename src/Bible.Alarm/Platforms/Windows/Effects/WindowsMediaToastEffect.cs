#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;

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
                return Task.CompletedTask;
            }

            // Only show if we have meaningful metadata
            if (string.IsNullOrWhiteSpace(action.Title) && string.IsNullOrWhiteSpace(action.Artist))
            {
                logger.Debug("Skipping media toast - no metadata available");
                return Task.CompletedTask;
            }

            logger.Information(
                "Showing media toast: Title={Title}, Artist={Artist}, Album={Album}, ArtworkUrl={ArtworkUrl}",
                action.Title,
                action.Artist,
                action.Album,
                action.ArtworkUrl);

            // Show rich toast notification with metadata
            // Include navigation controls and play/pause based on playback state
            var isPlaying = currentState.Status == PlayStatus.Playing;
            notificationService.ShowMediaToast(
                title: action.Title ?? "Now Playing",
                subtitle: action.Artist,
                body: action.Album,
                artworkUrl: action.ArtworkUrl,
                canPlayNext: currentState.CanPlayNext,
                canPlayPrevious: true, // Previous is always available (can restart current track)
                isPlaying: isPlaying);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing media toast notification");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
