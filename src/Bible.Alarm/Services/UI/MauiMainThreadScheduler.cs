#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Services.UI;

public sealed class MauiMainThreadScheduler : IMainThreadScheduler
{
    public bool IsMainThread => MainThread.IsMainThread;

    public void BeginInvokeOnMainThread(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
