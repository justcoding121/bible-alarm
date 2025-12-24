#nullable enable
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Services.UI;
using UIKit;

[assembly: Dependency(typeof(IOsToastService))]

namespace Bible.Alarm.Platforms.iOS.Services.UI;

public class IOsToastService(TaskScheduler taskScheduler) : ToastService, IDisposable
{
    private static readonly SemaphoreSlim @lock = new(1);

    public override async Task ShowMessage(string message, int seconds = 2)
    {
        if (clearRequest != null)
        {
            return;
        }

        if (!MainThread.IsMainThread)
        {
            await Task.Delay(0)
                .ContinueWith(async _ =>
                    await ShowAlert(message, seconds), taskScheduler);
        }
        else
        {
            await ShowAlert(message, seconds);
        }
    }

    private static async Task ShowAlert(string message, double seconds)
    {
        clearRequest = new TaskCompletionSource<bool>();

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            var containerView = GetContainerView();
            if (containerView == null)
            {
                return;
            }

            var toastView = CreateAndPositionToastView(message, containerView);
            AnimateToastIn(toastView);
            await WaitForToastDuration(seconds);
            await AnimateToastOutAndRemove(toastView);
        });

        clearRequest = null;
    }

    private static UIView? GetContainerView()
    {
        var window = GetKeyWindow();
        return window?.RootViewController?.View;
    }

    private static UIWindow? GetKeyWindow()
    {
        // Use modern API to get window from connected scenes (iOS 13+)
        // Minimum iOS version is 15.0, so this API is always available
        var scenes = UIApplication.SharedApplication.ConnectedScenes;
        if (scenes != null)
        {
            foreach (var scene in scenes)
            {
                if (scene is UIWindowScene windowScene && windowScene.Windows != null)
                {
                    var window = windowScene.Windows.FirstOrDefault(w => w.IsKeyWindow);
                    if (window != null)
                        return window;
                }
            }
        }

        return null;
    }

    private static UIView CreateAndPositionToastView(string message, UIView containerView)
    {
        // Create a non-blocking toast view
        var toastView = CreateToastView(message);
        containerView.AddSubview(toastView);

        // Position at bottom center
        toastView.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(
        [
            toastView.CenterXAnchor.ConstraintEqualTo(containerView.CenterXAnchor),
            toastView.BottomAnchor.ConstraintEqualTo(containerView.SafeAreaLayoutGuide.BottomAnchor, -50),
            toastView.LeadingAnchor.ConstraintGreaterThanOrEqualTo(containerView.LeadingAnchor, 20),
            toastView.TrailingAnchor.ConstraintLessThanOrEqualTo(containerView.TrailingAnchor, -20)
        ]);

        return toastView;
    }

    private static void AnimateToastIn(UIView toastView)
    {
        // Animate in
        toastView.Alpha = 0;
        UIView.Animate(0.3, () => toastView.Alpha = 1);
    }

    private static async Task WaitForToastDuration(double seconds)
    {
        // Wait for duration or clear request
        if (clearRequest != null)
        {
            await Task.WhenAny(clearRequest.Task, Task.Delay((int)(seconds * 1000)));
        }
        else
        {
            await Task.Delay((int)(seconds * 1000));
        }
    }

    private static async Task AnimateToastOutAndRemove(UIView toastView)
    {
        // Animate out and remove - ensure UIView operations run on main thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            UIView.Animate(0.3, () => toastView.Alpha = 0, () =>
            {
                toastView.RemoveFromSuperview();
                toastView.Dispose();
            });
        });
    }

    private static UIView CreateToastView(string message)
    {
        var label = CreateToastLabel(message);
        var containerView = CreateToastContainer();

        SetupToastLayout(label, containerView);

        return containerView;
    }

    private static UILabel CreateToastLabel(string message)
    {
        var label = new UILabel
        {
            Text = message,
            TextColor = UIColor.White,
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(16),
            Lines = 0,
            LineBreakMode = UILineBreakMode.WordWrap,
            BackgroundColor = UIColor.Black.ColorWithAlpha(0.8f)
        };

        label.Layer.CornerRadius = 10;
        label.Layer.MasksToBounds = true;

        return label;
    }

    private static UIView CreateToastContainer()
    {
        return new UIView
        {
            BackgroundColor = UIColor.Clear
        };
    }

    private static void SetupToastLayout(UILabel label, UIView containerView)
    {
        containerView.AddSubview(label);
        label.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(
        [
            label.TopAnchor.ConstraintEqualTo(containerView.TopAnchor, 12),
            label.BottomAnchor.ConstraintEqualTo(containerView.BottomAnchor, -12),
            label.LeadingAnchor.ConstraintEqualTo(containerView.LeadingAnchor, 16),
            label.TrailingAnchor.ConstraintEqualTo(containerView.TrailingAnchor, -16)
        ]);
    }

    private static TaskCompletionSource<bool>? clearRequest;

    public override Task Clear()
    {
        if (clearRequest != null)
        {
            clearRequest.SetResult(true);
        }

        return Task.CompletedTask;
    }
}
