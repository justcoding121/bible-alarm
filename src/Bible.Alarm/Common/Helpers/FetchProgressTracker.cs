#nullable enable
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Tracks fetch progress and updates UI properties.
/// </summary>
public sealed class FetchProgressTracker : IFetchProgress
{
    private readonly Action<double> updateProgress;
    private readonly Action<string> updateProgressText;
    private readonly Action<bool> setIsVisible;

    public FetchProgressTracker(
        Action<double> updateProgress,
        Action<string> updateProgressText,
        Action<bool> setIsVisible)
    {
        this.updateProgress = updateProgress;
        this.updateProgressText = updateProgressText;
        this.setIsVisible = setIsVisible;
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
        catch (Exception)
        {
            // ViewModel or UI element was disposed or other error, ignore the update
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
        catch (Exception)
        {
            // ViewModel or UI element was disposed or other error, ignore the update
        }
    }

    public void SetIsVisible(bool isVisible)
    {
        try
        {
            setIsVisible(isVisible);
        }
        catch (Exception)
        {
            // ViewModel or UI element was disposed or other error, ignore the update
        }
    }
}
