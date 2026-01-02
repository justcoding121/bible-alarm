#nullable enable
using Bible.Alarm.Common.Messenger;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmModal;

/// <summary>
/// Handles position updates and progress calculations for the alarm modal.
/// </summary>
public sealed class PositionManager(ILogger logger)
{
    private DateTime lastProgressUpdate = DateTime.MinValue;
    private const int ProgressUpdateThrottleMs = 100; // Throttle to max 10 updates per second

    /// <summary>
    /// Updates position from a playback position changed message.
    /// </summary>
    public void UpdatePositionFromMessage(
        PlaybackPositionChangedMessage message,
        TimeSpan currentDuration,
        Func<bool> shouldIgnorePositionUpdate,
        Action<string> setCurrentTime,
        Action<double> setProgress)
    {
        if (message.CurrentPosition.HasValue)
        {
            var position = message.CurrentPosition.Value;
            setCurrentTime($"{position.Minutes:00}:{position.Seconds:00}");

            if (currentDuration.TotalSeconds > 0)
            {
                var newProgress = position.TotalSeconds / currentDuration.TotalSeconds;
                if (Math.Abs(newProgress - GetCurrentProgress()) > 0.001)
                {
                    setProgress(newProgress);
                }
            }
            else
            {
                setProgress(0.0);
            }
        }
        else
        {
            setCurrentTime("00:00");
            setProgress(0.0);
        }
    }

    /// <summary>
    /// Handles preparation progress messages with throttling.
    /// </summary>
    public bool HandlePreparationProgressMessage(
        PlaybackPreparationProgressMessage message,
        Action<int, int, double, bool> updatePreparationState)
    {
        // Throttle progress updates to avoid flooding UI thread
        var now = DateTime.UtcNow;
        var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;

        if (timeSinceLastUpdate < ProgressUpdateThrottleMs && message.LoadedTracks < message.TotalTracks)
        {
            // Skip this update if it's too soon (but always process final update)
            return false;
        }

        lastProgressUpdate = now;

        // Handle high-frequency preparation progress updates via messaging
        // Use BeginInvokeOnMainThread to queue on UI thread without blocking
        var loadedTracks = message.LoadedTracks;
        var totalTracks = message.TotalTracks;
        var preparationProgress = totalTracks > 0 ? loadedTracks / (double)totalTracks : 0.0;
        var isPreparing = totalTracks > 0 && loadedTracks < totalTracks;

        updatePreparationState(loadedTracks, totalTracks, preparationProgress, isPreparing);
        return true;
    }

    /// <summary>
    /// Checks if a position update should be ignored based on current progress.
    /// </summary>
    public bool ShouldIgnorePositionUpdate(double newProgress, double currentProgress, TimeSpan currentDuration)
    {
        if (currentDuration.TotalSeconds <= 0)
        {
            return false;
        }

        var actualProgress = newProgress;
        return Math.Abs(actualProgress - currentProgress) < 0.01; // Small threshold for ignoring
    }

    /// <summary>
    /// Gets the current progress value (this would need to be passed in from the main class).
    /// </summary>
    private double GetCurrentProgress()
    {
        // This would need to be passed in or accessed differently
        // For now, returning 0 as a placeholder
        return 0.0;
    }

    /// <summary>
    /// Calculates formatted time string from TimeSpan.
    /// </summary>
    public string FormatTime(TimeSpan timeSpan)
    {
        return $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
    }

    /// <summary>
    /// Calculates progress from position and duration.
    /// </summary>
    public double CalculateProgress(TimeSpan position, TimeSpan duration)
    {
        return duration.TotalSeconds > 0 ? position.TotalSeconds / duration.TotalSeconds : 0.0;
    }
}
