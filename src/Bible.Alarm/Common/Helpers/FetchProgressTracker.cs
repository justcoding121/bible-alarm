#nullable enable
using System.Threading;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Tracks fetch progress and updates UI properties.
/// Supports cancellation via CancellationToken.
/// </summary>
public sealed class FetchProgressTracker : IFetchProgress
{
    private readonly Action<double> updateProgress;
    private readonly Action<string> updateProgressText;
    private readonly Action<bool> setIsVisible;

    /// <summary>
    /// Gets the cancellation token for this fetch operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    public FetchProgressTracker(
        Action<double> updateProgress,
        Action<string> updateProgressText,
        Action<bool> setIsVisible,
        CancellationToken cancellationToken = default)
    {
        this.updateProgress = updateProgress;
        this.updateProgressText = updateProgressText;
        this.setIsVisible = setIsVisible;
        this.CancellationToken = cancellationToken;
    }

    public void UpdateProgress(double progress)
    {
        try
        {
            // Clamp progress between 0.0 and 1.0
            var clampedProgress = Math.Max(0.0, Math.Min(1.0, progress));
            updateProgress(clampedProgress);
            
            // Automatically update text with percentage
            var percent = (int)Math.Round(clampedProgress * 100);
            updateProgressText($"{percent}%");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.FetchProgressTrackerDiagnosticsLog.UpdateProgressUiUpdateFailedElementMayBeDisposed);
        }
    }

    public void UpdateProgressText(string text)
    {
        try
        {
            // This method is kept for compatibility but should not be used
            // Progress text is automatically set by UpdateProgress
            updateProgressText(text);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.FetchProgressTrackerDiagnosticsLog.UpdateProgressTextUiUpdateFailedElementMayBeDisposed);
        }
    }

    public void SetIsVisible(bool isVisible)
    {
        try
        {
            setIsVisible(isVisible);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.FetchProgressTrackerDiagnosticsLog.SetIsVisibleUiUpdateFailedElementMayBeDisposed);
        }
    }
}
