#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services.UI;

public sealed class MauiMainThreadRunner : IMainThreadRunner
{
    public Task InvokeOnMainThreadAsync(Func<Task> funcTask)
    {
        // Queuing while already on the main thread deadlocks: the caller waits for a turn
        // that cannot start until this call returns. Startup hits that path after the first await.
        if (MainThread.IsMainThread)
        {
            return funcTask();
        }

        return MainThread.InvokeOnMainThreadAsync(funcTask);
    }

    public void BeginInvokeOnMainThread(Action action) =>
        MainThread.BeginInvokeOnMainThread(action);
}
