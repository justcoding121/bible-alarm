#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Services.UI;

public sealed class MauiNavigationUiThreadInvoker : INavigationUiThreadInvoker
{
    public Task InvokeOnUiThreadAsync(Func<Task> work)
    {
#if WINDOWS
        var app = Application.Current;
        var winDispatcher = (app?.Windows.Count > 0 && app.Windows[0].Page is NavigationPage navPage)
            ? navPage.Dispatcher
            : app?.Dispatcher;
        if (winDispatcher != null)
        {
            if (winDispatcher.IsDispatchRequired)
            {
                return winDispatcher.DispatchAsync(work);
            }

            return work();
        }
#endif
        return MainThread.InvokeOnMainThreadAsync(work);
    }
}
