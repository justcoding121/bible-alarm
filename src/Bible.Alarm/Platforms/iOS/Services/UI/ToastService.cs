using Bible.Alarm.Services.iOS;
using UIKit;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(IOsToastService))]

namespace Bible.Alarm.Services.iOS
{
    public class IOsToastService(TaskScheduler taskScheduler) : ToastService, IDisposable
    {
        private static SemaphoreSlim @lock = new SemaphoreSlim(1);

        public override async Task ShowMessage(string message, int seconds)
        {
            if (clearRequest != null)
            {
                return;
            }

            if (!MainThread.IsMainThread)
            {
                await Task.Delay(0)
                    .ContinueWith(async (x) =>
                        await ShowAlert(message, (double)seconds), taskScheduler);
            }
            else
            {
                await ShowAlert(message, (double)seconds);
            }
        }

        private async Task ShowAlert(string message, double seconds)
        {
            clearRequest = new TaskCompletionSource<bool>();
            await @lock.WaitAsync();

            try
            {
                var alert = UIAlertController.Create(null, message, UIAlertControllerStyle.Alert);
#pragma warning disable CA1422
                UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(alert, true, null);
#pragma warning restore CA1422

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