using Bible.Alarm.Platforms.iOS.Services.UI;
using UIKit;

[assembly: Dependency(typeof(IOsToastService))]

namespace Bible.Alarm.Platforms.iOS.Services.UI
{
    public class IOsToastService(TaskScheduler taskScheduler) : ToastService, IDisposable
    {
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

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
                        await ShowAlert(message, seconds), taskScheduler);
            }
            else
            {
                await ShowAlert(message, seconds);
            }
        }

        private async Task ShowAlert(string message, double seconds)
        {
            clearRequest = new TaskCompletionSource<bool>();
            await Lock.WaitAsync();

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
                Lock.Release();
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