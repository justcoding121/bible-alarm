#nullable enable

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Interface for tracking fetch progress with percentage updates.
/// </summary>
public interface IFetchProgress
{
    /// <summary>
    /// Updates the progress percentage (0.0 to 1.0).
    /// </summary>
    void UpdateProgress(double progress);

    /// <summary>
    /// Updates the progress text (e.g., "Loading...", "45.2%").
    /// </summary>
    void UpdateProgressText(string text);

    /// <summary>
    /// Sets whether the progress indicator should be visible.
    /// </summary>
    void SetIsVisible(bool isVisible);
}
