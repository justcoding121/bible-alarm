using Android.Widget;
using Bible.Alarm.Services.UI;
using AndroidApplication = Android.App.Application;


namespace Bible.Alarm.Platforms.Android.Services.UI;

public class DroidToastService(TaskScheduler taskScheduler) : ToastService, IDisposable
{
    private readonly TaskScheduler _taskScheduler = taskScheduler;
    private static readonly SemaphoreSlim Lock = new(1);
    private static Toast latest;

    public override async Task ShowMessage(string message, int seconds)
    {
        await Lock.WaitAsync();

        try
        {
            //if current is not UI thread, run on UI thread
            if (!MainThread.IsMainThread)
                await Task.Delay(0)
                    .ContinueWith(_ =>
                        ShowToast(message, seconds), _taskScheduler);
            else
                ShowToast(message, seconds);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static void ShowToast(string message, int seconds)
    {
        var context = AndroidApplication.Context;

        if (seconds <= 3)
            latest = Toast.MakeText(context, message, ToastLength.Short);
        else
            latest = Toast.MakeText(context, message, ToastLength.Long);

        latest.Show();
    }

    //not needed for android
    public override async Task Clear()
    {
        await Lock.WaitAsync();

        try
        {
            if (!MainThread.IsMainThread)
                await Task.Delay(0)
                    .ContinueWith(_ =>
                        latest?.Cancel(), _taskScheduler);
            else
                latest?.Cancel();
        }
        finally
        {
            Lock.Release();
        }
    }
}