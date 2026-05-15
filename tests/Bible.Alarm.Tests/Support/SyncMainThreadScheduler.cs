#nullable enable

using Bible.Alarm.Services.UI.Interfaces;

namespace Bible.Alarm.Tests.Support;

/// <summary>
/// Runs UI work synchronously (headless tests without a WinUI dispatcher).
/// </summary>
public sealed class SyncMainThreadScheduler : IMainThreadScheduler
{
    public bool IsMainThread => true;

    public void BeginInvokeOnMainThread(Action action) => action();

    public Task InvokeOnMainThreadAsync(Func<Task> work) => work();
}

/// <summary>
/// Simulates a background thread: <see cref="IMainThreadScheduler.IsMainThread"/> is false, but
/// <see cref="IMainThreadScheduler.BeginInvokeOnMainThread"/> still runs work synchronously for tests.
/// </summary>
public sealed class OffMainThreadSyncScheduler : IMainThreadScheduler
{
    public bool IsMainThread => false;

    public void BeginInvokeOnMainThread(Action action) => action();

    public Task InvokeOnMainThreadAsync(Func<Task> work) => work();
}
