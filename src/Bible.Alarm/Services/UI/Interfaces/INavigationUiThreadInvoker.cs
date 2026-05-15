#nullable enable

namespace Bible.Alarm.Services.UI.Interfaces;

/// <summary>
/// Marshals navigation-related async work to the UI thread (WinUI dispatcher on Windows, MAUI MainThread elsewhere).
/// </summary>
public interface INavigationUiThreadInvoker
{
    Task InvokeOnUiThreadAsync(Func<Task> work);
}
