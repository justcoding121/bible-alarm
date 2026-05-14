#nullable enable

namespace Bible.Alarm.Services.UI.Interfaces;

/// <summary>
/// Abstraction over MAUI main-thread marshaling so headless tests can execute playback-modal UI work inline.
/// </summary>
public interface IMainThreadRunner
{
    Task InvokeOnMainThreadAsync(Func<Task> funcTask);

    void BeginInvokeOnMainThread(Action action);
}
