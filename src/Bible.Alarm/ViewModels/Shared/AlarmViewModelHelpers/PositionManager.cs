#nullable enable
using Bible.Alarm.Common.Messenger;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles position updates and progress calculations for the alarm modal.
/// </summary>
public sealed class PositionManager()
{
    private DateTime lastProgressUpdate = DateTime.MinValue;
    // Throttle to max 10 updates per second
    private const int ProgressUpdateThrottleMs = 100;

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

        // IMPORTANT:
        // We must not throttle the "start preparing" transition, otherwise quick next/prev actions
        // can drop the first preparing message and the alarm modal won't show the progress UI at all.
        // This is especially noticeable when the download service doesn't provide incremental progress
        // (e.g., waiting on an existing in-progress download), because only start+finish messages may be emitted.
        var isStartPreparingMessage =
            message.TotalTracks > 0 &&
            message.LoadedTracks == 0 &&
            message.TotalBytesDownloaded == 0;

        // Never throttle when byte progress has reached 100% (e.g. cached file reports complete).
        // Otherwise, fast flows (cached or very quick download) drop the 100% update and the user
        // only sees 0% then progress disappearing.
        var isByteCompleteMessage =
            message.TotalBytesExpected.HasValue &&
            message.TotalBytesExpected.Value > 0 &&
            message.TotalBytesDownloaded >= message.TotalBytesExpected.Value;

        if (!isStartPreparingMessage &&
            !isByteCompleteMessage &&
            timeSinceLastUpdate < ProgressUpdateThrottleMs &&
            message.LoadedTracks < message.TotalTracks)
        {
            // Skip this update if it's too soon (but always process final update)
            return false;
        }

        lastProgressUpdate = now;

        // Handle high-frequency preparation progress updates via messaging
        // Use BeginInvokeOnMainThread to queue on UI thread without blocking
        var loadedTracks = message.LoadedTracks;
        var totalTracks = message.TotalTracks;
        
        // Calculate progress based on overall bytes downloaded (for parallel downloads)
        // Use byte-based progress if available, otherwise fall back to track-based progress
        double preparationProgress;
        if (message.TotalBytesExpected.HasValue && message.TotalBytesExpected.Value > 0)
        {
            // Use overall byte-based progress for accurate representation of parallel downloads
            preparationProgress = (double)message.TotalBytesDownloaded / message.TotalBytesExpected.Value;
            // Clamp to [0, 1]
            preparationProgress = Math.Max(0.0, Math.Min(1.0, preparationProgress));
        }
        else if (totalTracks > 0)
        {
            // Fallback to track-based progress if byte info not available
            var completedTrackProgress = loadedTracks / (double)totalTracks;
            var currentTrackContribution = message.CurrentTrackProgress / totalTracks;
            preparationProgress = completedTrackProgress + currentTrackContribution;
        }
        else
        {
            preparationProgress = 0.0;
        }
        
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
        // Small threshold for ignoring
        return Math.Abs(actualProgress - currentProgress) < 0.01;
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
