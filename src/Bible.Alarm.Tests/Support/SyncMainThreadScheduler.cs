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
}
