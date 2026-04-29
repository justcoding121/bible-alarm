#nullable enable
using System.Runtime.InteropServices;
using Bible.Alarm.Platforms.iOS.Helpers;
using Bible.Alarm.Platforms.iOS.Services.UI;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.ToastLayoutHelpers;
using Serilog;
using UIKit;

[assembly: Dependency(typeof(IOsToastService))]

namespace Bible.Alarm.Platforms.iOS.Services.UI;

/// <summary>
/// Custom in-app toast for iOS. Uses a native UIView overlay.
/// When a new toast arrives while one is showing, the old toast is dismissed immediately.
/// </summary>
public class IOsToastService : ToastService, IDisposable
{
    private static readonly SemaphoreSlim @lock = new(1);
    private static CancellationTokenSource? activeCts;
    private static UIView? currentToastView;

    public override async Task ShowMessage(string message, int seconds = 2)
    {
        CancelActiveCts();

        await @lock.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ShowAlert(message, seconds);
            });
        }
        finally
        {
            @lock.Release();
        }
    }

    private static async Task ShowAlert(string message, double seconds)
    {
        var cts = new CancellationTokenSource();
        activeCts = cts;

        try
        {
            RemoveCurrentToast();

            var containerView = GetContainerView();
            if (containerView == null)
            {
                return;
            }

            var toastView = CreateAndPositionToastView(message, containerView, ToastMiniBarInsetHelper.GetBottomInsetDip());
            currentToastView = toastView;
            AnimateToastIn(toastView);

            try
            {
                await Task.Delay((int)(seconds * 1000), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await AnimateToastOutAndRemove(toastView);
        }
        finally
        {
            if (activeCts == cts)
            {
                activeCts = null;
            }
        }
    }

    private static void CancelActiveCts()
    {
        var existing = activeCts;
        if (existing == null)
        {
            return;
        }

        activeCts = null;
        try
        {
            existing.Cancel();
            existing.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void RemoveCurrentToast()
    {
        if (currentToastView == null)
        {
            return;
        }

        try
        {
            currentToastView.Layer.RemoveAllAnimations();
            currentToastView.RemoveFromSuperview();
            IOSNativeViewCleanupHelper.SuppressFinalizersForViewHierarchy(currentToastView);
            currentToastView.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "IOsToastService: RemoveCurrentToast cleanup failed (non-fatal)");
        }

        currentToastView = null;
    }

    private static UIView? GetContainerView()
    {
        var window = GetKeyWindow();
        return window?.RootViewController?.View;
    }

    private static UIWindow? GetKeyWindow()
    {
        var scenes = UIApplication.SharedApplication.ConnectedScenes;
        if (scenes != null)
        {
            foreach (var scene in scenes)
            {
                if (scene is UIWindowScene windowScene && windowScene.Windows != null)
                {
                    foreach (UIWindow w in windowScene.Windows)
                    {
                        if (w.IsKeyWindow)
                        {
                            return w;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static UIView CreateAndPositionToastView(string message, UIView containerView, double miniBarInsetDip)
    {
        var toastView = CreateToastView(message);
        containerView.AddSubview(toastView);

        var bottomMargin = (NFloat)(50 + miniBarInsetDip);
        toastView.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(
        [
            toastView.CenterXAnchor.ConstraintEqualTo(containerView.CenterXAnchor),
            toastView.BottomAnchor.ConstraintEqualTo(containerView.SafeAreaLayoutGuide.BottomAnchor, -bottomMargin),
            toastView.LeadingAnchor.ConstraintGreaterThanOrEqualTo(containerView.LeadingAnchor, 20),
            toastView.TrailingAnchor.ConstraintLessThanOrEqualTo(containerView.TrailingAnchor, -20)
        ]);

        return toastView;
    }

    private static void AnimateToastIn(UIView toastView)
    {
        toastView.Alpha = 0;
        UIView.Animate(0.3, () => toastView.Alpha = 1);
    }

    private static async Task AnimateToastOutAndRemove(UIView toastView)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            UIView.Animate(0.3, () => toastView.Alpha = 0, () =>
            {
                toastView.RemoveFromSuperview();
                IOSNativeViewCleanupHelper.SuppressFinalizersForViewHierarchy(toastView);
                toastView.Dispose();

                if (currentToastView == toastView)
                {
                    currentToastView = null;
                }
            });
        });
    }

    private static UIView CreateToastView(string message)
    {
        var label = CreateToastLabel(message);
        var backgroundView = CreateToastBackground();
        var containerView = CreateToastContainer();

        containerView.AddSubview(backgroundView);
        backgroundView.TranslatesAutoresizingMaskIntoConstraints = false;

        backgroundView.AddSubview(label);
        label.TranslatesAutoresizingMaskIntoConstraints = false;

        NSLayoutConstraint.ActivateConstraints(
        [
            backgroundView.TopAnchor.ConstraintEqualTo(containerView.TopAnchor),
            backgroundView.BottomAnchor.ConstraintEqualTo(containerView.BottomAnchor),
            backgroundView.LeadingAnchor.ConstraintEqualTo(containerView.LeadingAnchor),
            backgroundView.TrailingAnchor.ConstraintEqualTo(containerView.TrailingAnchor)
        ]);

        NSLayoutConstraint.ActivateConstraints(
        [
            label.TopAnchor.ConstraintEqualTo(backgroundView.TopAnchor, 12),
            label.BottomAnchor.ConstraintEqualTo(backgroundView.BottomAnchor, -12),
            label.LeadingAnchor.ConstraintEqualTo(backgroundView.LeadingAnchor, 16),
            label.TrailingAnchor.ConstraintEqualTo(backgroundView.TrailingAnchor, -16)
        ]);

        return containerView;
    }

    private static UILabel CreateToastLabel(string message)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var textColor = GetToastTextColor();

        return new UILabel
        {
            Text = message,
            TextColor = textColor,
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.SystemFontOfSize(16),
            Lines = 0,
            LineBreakMode = UILineBreakMode.WordWrap,
            BackgroundColor = UIColor.Clear
        };
    }

    private static UIView CreateToastBackground()
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var backgroundColor = GetToastBackgroundColor(theme);

        var backgroundView = new UIView
        {
            BackgroundColor = backgroundColor
        };

        backgroundView.Layer.CornerRadius = 10;
        backgroundView.Layer.MasksToBounds = true;

        return backgroundView;
    }

    private static UIColor GetToastBackgroundColor(AppTheme theme)
    {
        if (theme == AppTheme.Dark)
        {
            return UIColor.FromRGBA(0x2A / 255.0f, 0x2A / 255.0f, 0x2A / 255.0f, 0.95f);
        }
        else
        {
            return UIColor.Black.ColorWithAlpha(0.8f);
        }
    }

    private static UIColor GetToastTextColor() => UIColor.White;

    private static UIView CreateToastContainer()
    {
        return new UIView
        {
            BackgroundColor = UIColor.Clear
        };
    }

    public override Task Clear()
    {
        CancelActiveCts();

        MainThread.BeginInvokeOnMainThread(() => RemoveCurrentToast());

        return Task.CompletedTask;
    }
}
