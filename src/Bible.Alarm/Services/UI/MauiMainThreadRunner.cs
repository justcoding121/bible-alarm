#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services.UI;

public sealed class MauiMainThreadRunner : IMainThreadRunner
{
    public Task InvokeOnMainThreadAsync(Func<Task> funcTask) =>
        MainThread.InvokeOnMainThreadAsync(funcTask);

    public void BeginInvokeOnMainThread(Action action) =>
        MainThread.BeginInvokeOnMainThread(action);
}
