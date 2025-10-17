using Bible.Alarm.Services.iOS;
using System;
using System.Threading;
using System.Threading.Tasks;
using UIKit;
using Xamarin.Essentials;

[assembly: Xamarin.Forms.Dependency(typeof(iOSToastService))]
namespace Bible.Alarm.Services.iOS
{
    public class iOSToastService : ToastService, IDisposable
    {
        private readonly TaskScheduler taskScheduler;

        public iOSToastService(TaskScheduler taskScheduler)
        {
            this.taskScheduler = taskScheduler;
        }

        private static SemaphoreSlim @lock = new SemaphoreSlim(1);
        public async override Task ShowMessage(string message, int seconds)
        {
            if (clearRequest != null)
            {
                return;
            }

            if (!MainThread.IsMainThread)
            {
               await Task.Delay(0)
                 .ContinueWith(async (x) =>
                     await showAlert(message, (double)seconds), taskScheduler);
            }
            else
            {
                await showAlert(message, (double)seconds);
            }
        }

        private async Task showAlert(string message, double seconds)
        {
            clearRequest = new TaskCompletionSource<bool>();
            await @lock.WaitAsync();
         
            try
            {
            
                var alert = UIAlertController.Create(null, message, UIAlertControllerStyle.Alert);
                UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(alert, true, null);

                await Task.WhenAny(clearRequest.Task, Task.Delay((int)(seconds * 1000))).ConfigureAwait(true);
                alert.DismissViewController(true, null);
               
            }
            finally
            {
                @lock.Release();
                clearRequest = null;
            }
        }

        private static TaskCompletionSource<bool> clearRequest;
        public override Task Clear()
        {
            if (clearRequest != null)
            {
                clearRequest.SetResult(true);
            }

            return Task.CompletedTask;
        }
    }
}
