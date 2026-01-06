#nullable enable
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles system media controls (notification/lockscreen) button presses.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class SystemControlsHandler
{
    private readonly ILogger logger;

    public SystemControlsHandler(ILogger logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Handles Next button press message from Android system media controls (notification/lockscreen).
    /// Calls the provided action on UI thread with a delay to let MediaSession finish processing.
    /// </summary>
    public void HandleNextButton(Func<Task> playNextAsync)
    {
        logger.Debug("Next button pressed from system controls - calling PlayNextAsync");
        // Add delay to let MediaSession finish processing the button press
        // This prevents IllegalStateException when ExoPlayer is transitioning
        Task.Run(async () =>
        {
            // Delay to let MediaSession finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await playNextAsync();
            });
        });
    }

    /// <summary>
    /// Handles Previous button press message from Android system media controls (notification/lockscreen).
    /// Calls the provided action on UI thread with a delay to let MediaSession finish processing.
    /// </summary>
    public void HandlePreviousButton(Func<Task> playPreviousAsync)
    {
        logger.Debug("Previous button pressed from system controls - calling PlayPreviousAsync");
        // Add delay to let MediaSession finish processing the button press
        // This prevents IllegalStateException when ExoPlayer is transitioning
        Task.Run(async () =>
        {
            // Delay to let MediaSession finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await playPreviousAsync();
            });
        });
    }

    /// <summary>
    /// Handles Play button press message from system media controls (notification/lockscreen).
    /// Calls the provided action on UI thread with a delay to let system controls finish processing.
    /// </summary>
    public void HandlePlayButton(Func<Task> playAsync)
    {
        logger.Debug("Play button pressed from system controls - calling PlayAsync");
        Task.Run(async () =>
        {
            // Delay to let system controls finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await playAsync();
            });
        });
    }

    /// <summary>
    /// Handles Pause button press message from system media controls (notification/lockscreen).
    /// Calls the provided action on UI thread with a delay to let system controls finish processing.
    /// </summary>
    public void HandlePauseButton(Func<Task> pauseAsync)
    {
        logger.Debug("Pause button pressed from system controls - calling PauseAsync");
        Task.Run(async () =>
        {
            // Delay to let system controls finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await pauseAsync();
            });
        });
    }
}

