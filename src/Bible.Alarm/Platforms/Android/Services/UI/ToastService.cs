using Android.Widget;
using Bible.Alarm.Droid;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
// using Bible.Alarm.Services.Droid.Extensions; // Removed - no longer needed

namespace Bible.Alarm.Services.Droid
{
    public class DroidToastService() : ToastService, IDisposable
    {
        private readonly TaskScheduler _taskScheduler = ServiceProviderManager.GetService<TaskScheduler>();
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private static Toast latest;

        public override async Task ShowMessage(string message, int seconds)
        {
            await Lock.WaitAsync();

            try
            {

                //if current is not UI thread, run on UI thread
                if (!MainThread.IsMainThread)
                {
                    await Task.Delay(0)
                    .ContinueWith((x) =>
                        ShowToast(message, seconds), _taskScheduler);
                }
                else
                {
                    ShowToast(message, seconds);
                }

            }
            finally
            {
                Lock.Release();
            }
        }

        private void ShowToast(string message, int seconds)
        {
            var context = container.AndroidContext();

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
            await Lock.WaitAsync();

            try
            {
                if (!MainThread.IsMainThread)
                {
                    await Task.Delay(0)
                    .ContinueWith((x) =>
                        latest?.Cancel(), _taskScheduler);
                }
                else
                {
                    latest?.Cancel();
                }
            }
            finally
            {
                Lock.Release();
            }
        }
    }
}
