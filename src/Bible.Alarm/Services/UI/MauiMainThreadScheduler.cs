#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services.UI;

public sealed class MauiMainThreadScheduler : IMainThreadScheduler
{
    public bool IsMainThread => MainThread.IsMainThread;

    public void BeginInvokeOnMainThread(Action action) => MainThread.BeginInvokeOnMainThread(action);

    public Task InvokeOnMainThreadAsync(Func<Task> work)
    {
        // Same re-entrancy rule as MauiMainThreadRunner: do not wait for the main thread from the main thread.
        if (MainThread.IsMainThread)
        {
            return work();
        }

        return MainThread.InvokeOnMainThreadAsync(work);
    }
}
