using Android.Widget;
using Bible.Alarm.Droid;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace Bible.Alarm.Services.Droid
{
    public class DroidToastService : ToastService, IDisposable
    {
        private readonly IContainer container;
        private readonly TaskScheduler taskScheduler;
        private static readonly SemaphoreSlim @lock = new SemaphoreSlim(1);
        private static Toast latest;
        public DroidToastService(IContainer container)
        {
            this.container = container;
            this.taskScheduler = container.Resolve<TaskScheduler>();
        }
        public async override Task ShowMessage(string message, int seconds)
        {
            await @lock.WaitAsync();

            try
            {

                //if current is not UI thread, run on UI thread
                if (!MainThread.IsMainThread)
                {
                    await Task.Delay(0)
                    .ContinueWith((x) =>
                        showToast(message, seconds), taskScheduler);
                }
                else
                {
                    showToast(message, seconds);
                }

            }
            finally
            {
                @lock.Release();
            }
        }

        private void showToast(string message, int seconds)
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
        public async override Task Clear()
        {
            await @lock.WaitAsync();

            try
            {
                if (!MainThread.IsMainThread)
                {
                    await Task.Delay(0)
                    .ContinueWith((x) =>
                        latest?.Cancel(), taskScheduler);
                }
                else
                {
                    latest?.Cancel();
                }
            }
            finally
            {
                @lock.Release();
            }
        }
    }
}
