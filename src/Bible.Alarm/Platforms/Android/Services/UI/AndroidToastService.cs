using Android.Widget;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.UI;
using AndroidApplication = Android.App.Application;


namespace Bible.Alarm.Platforms.Android.Services.UI;

public class AndroidToastService(TaskScheduler taskScheduler) : ToastService, IDisposable
{
    private static readonly SemaphoreSlim @lock = new(1);
    private static Toast latest;

    public override async Task ShowMessage(string message, int seconds)
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            //if current is not UI thread, run on UI thread
            if (!MainThread.IsMainThread)
            {
                await Task.Delay(0)
                    .ContinueWith(_ =>
                        ShowToast(message, seconds), taskScheduler);
            }
            else
            {
                ShowToast(message, seconds);
            }
        });
    }

    private static void ShowToast(string message, int seconds)
    {
        var context = AndroidApplication.Context;

        if (seconds <= 3)
        {
            latest = Toast.MakeText(context, message, ToastLength.Short);
        }
        else
        {
            latest = Toast.MakeText(context, message, ToastLength.Long);
        }

        latest.Show();
    }

    //not needed for android
    public override async Task Clear()
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (!MainThread.IsMainThread)
            {
                await Task.Delay(0)
                    .ContinueWith(_ =>
                        latest?.Cancel(), taskScheduler);
            }
            else
            {
                latest?.Cancel();
            }
        });
    }
}
