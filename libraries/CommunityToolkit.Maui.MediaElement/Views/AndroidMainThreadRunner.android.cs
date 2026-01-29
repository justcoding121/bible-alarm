#nullable enable

using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace CommunityToolkit.Maui.Core.Views;

internal static class AndroidMainThreadRunner
{
    internal static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        Exception? ex = null;
        using var evt = new ManualResetEventSlim(false);
        var mainLooper = Looper.MainLooper ?? throw new InvalidOperationException("MainLooper is null");
        var handler = new Handler(mainLooper);
        handler.Post(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
            finally
            {
                evt.Set();
            }
        });

        evt.Wait();
        if (ex != null)
        {
            throw ex;
        }
    }
}

