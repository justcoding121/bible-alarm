#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using System.Collections.Generic;

namespace Bible.Alarm.Tests.Support;

/// <summary>
/// Runs playback-modal main-thread delegates inline so unit tests execute without a WinUI dispatcher.
/// </summary>
public sealed class InlineMainThreadRunner : IMainThreadRunner
{
    private readonly List<Task> pending = [];

    public Task InvokeOnMainThreadAsync(Func<Task> funcTask)
    {
        var t = funcTask();
        pending.Add(t);
        return t;
    }

    public void BeginInvokeOnMainThread(Action action) =>
        action();

    public async Task DrainAsync()
    {
        while (pending.Count > 0)
        {
            var t = pending[0];
            pending.RemoveAt(0);
            await t;
        }
    }
}
