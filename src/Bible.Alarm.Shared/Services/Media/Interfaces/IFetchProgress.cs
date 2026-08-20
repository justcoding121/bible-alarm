#nullable enable

using System.Threading;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

public interface IFetchProgress
{
    /// <summary>
    /// Updates the progress percentage (0.0 to 1.0).
    /// </summary>
    void UpdateProgress(double progress);

    /// <summary>
    /// Updates the progress text (e.g., "45%").
    /// </summary>
    void UpdateProgressText(string text);

    void SetIsVisible(bool isVisible);

    /// <summary>
    /// Gets the cancellation token for this fetch operation.
    /// Operations should check this token and throw OperationCanceledException when cancelled.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
