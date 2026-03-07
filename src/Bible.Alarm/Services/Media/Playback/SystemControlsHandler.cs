#nullable enable
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
        // Short delay (50ms) to let MediaSession finish processing without letting the dummy
        // track end. A longer delay would let MediaEnded fire first, then our PlayNextAsync
        // would run and advance again, skipping the target track and breaking resume.
        Task.Run(async () =>
        {
            await Task.Delay(50);
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
        // Short delay (50ms) to let MediaSession finish processing.
        Task.Run(async () =>
        {
            await Task.Delay(50);
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

    /// <summary>
    /// Handles Toggle Play/Pause button press (e.g. single button headphones, CarPlay).
    /// Caller provides the toggle logic (pause if playing, play/start default otherwise).
    /// </summary>
    public void HandleTogglePlayPause(Func<Task> toggleAsync)
    {
        logger.Debug("Toggle play/pause pressed from system controls");
        Task.Run(async () =>
        {
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(toggleAsync);
        });
    }

    /// <summary>
    /// Handles Fast Forward button press message from system media controls (notification/lockscreen).
    /// Calls the provided action on UI thread with a delay to let system controls finish processing.
    /// </summary>
    public void HandleSeekForwardButton(Func<Task> seekForwardAsync)
    {
        logger.Debug("Fast Forward button pressed from system controls - calling SeekForwardAsync");
        Task.Run(async () =>
        {
            // Delay to let system controls finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await seekForwardAsync();
            });
        });
    }

    /// <summary>
    /// Handles Rewind button press message from system media controls (notification/lockscreen).
    /// Calls the provided action on UI thread with a delay to let system controls finish processing.
    /// </summary>
    public void HandleSeekBackwardButton(Func<Task> seekBackwardAsync)
    {
        logger.Debug("Rewind button pressed from system controls - calling SeekBackwardAsync");
        Task.Run(async () =>
        {
            // Delay to let system controls finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await seekBackwardAsync();
            });
        });
    }
}

