#nullable enable

namespace Bible.Alarm.Services.UI.Interfaces;

/// <summary>
/// Abstraction over MAUI <c>MainThread</c> for view-model and helper code that must marshal to the UI thread.
/// </summary>
public interface IMainThreadScheduler
{
    bool IsMainThread { get; }

    void BeginInvokeOnMainThread(Action action);

    /// <summary>
    /// Marshals async work to the UI thread (MAUI <c>MainThread</c> on production).
    /// </summary>
    Task InvokeOnMainThreadAsync(Func<Task> work);
}
