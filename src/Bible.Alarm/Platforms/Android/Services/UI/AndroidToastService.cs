#nullable enable

using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.ToastLayoutHelpers;
using Serilog;
using Color = Android.Graphics.Color;
using View = Android.Views.View;

namespace Bible.Alarm.Platforms.Android.Services.UI;

/// <summary>
/// Custom in-app toast for Android. Native Toast.setGravity() is ignored on API 30+,
/// so we add a custom view overlay to the activity's content and position it ourselves.
/// When a new toast arrives while one is showing, the old toast is dismissed immediately.
/// </summary>
public class AndroidToastService : ToastService, IDisposable
{
    private static readonly ILogger logger = Log.ForContext<AndroidToastService>();
    private static readonly SemaphoreSlim @lock = new(1);
    private static CancellationTokenSource? activeCts;
    private static View? currentToastView;

    private const int BaseBottomMarginDp = 50;
    private const int HorizontalMarginDp = 20;
    private const int PaddingHorizontalDp = 16;
    private const int PaddingVerticalDp = 12;
    private const int CornerRadiusDp = 10;
    private const int TextSizeSp = 16;
    private const int FadeAnimationMs = 300;

    public override async Task ShowMessage(string message, int seconds = 2)
    {
        CancelActiveCts();

        await @lock.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ShowCustomToast(message, seconds);
            });
        }
        finally
        {
            @lock.Release();
        }
    }

    private static async Task ShowCustomToast(string message, double seconds)
    {
        var cts = new CancellationTokenSource();
        activeCts = cts;

        try
        {
            RemoveCurrentToast();

            var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var rootView = activity?.FindViewById<FrameLayout>(global::Android.Resource.Id.Content);
            if (rootView == null)
            {
                return;
            }

            var density = activity!.Resources?.DisplayMetrics?.Density ?? 1f;
            var toastView = CreateToastView(activity, message, density);
            var layoutParams = CreateLayoutParams(density);

            rootView.AddView(toastView, layoutParams);
            currentToastView = toastView;

            AnimateIn(toastView);

            try
            {
                await Task.Delay((int)(seconds * 1000), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await AnimateOutAndRemove(toastView, rootView);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error showing custom toast");
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
            // Cancel/Dispose may race when toast is already torn down.
        }
    }

    private static TextView CreateToastView(global::Android.App.Activity activity, string message, float density)
    {
        var theme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var backgroundColor = GetBackgroundColor(theme);
        var textColor = GetTextColor();

        var background = new GradientDrawable();
        background.SetCornerRadius(CornerRadiusDp * density);
        background.SetColor(backgroundColor);

        var textView = new TextView(activity)
        {
            Text = message,
            Gravity = GravityFlags.Center,
        };

        textView.SetTextColor(textColor);
        textView.SetTextSize(global::Android.Util.ComplexUnitType.Sp, TextSizeSp);
        textView.SetMaxWidth((int)(400 * density));
        textView.Background = background;

        var padH = (int)(PaddingHorizontalDp * density);
        var padV = (int)(PaddingVerticalDp * density);
        textView.SetPadding(padH, padV, padH, padV);

        textView.Clickable = false;
        textView.Focusable = false;

        return textView;
    }

    private static FrameLayout.LayoutParams CreateLayoutParams(float density)
    {
        var miniBarInset = ToastMiniBarInsetHelper.GetBottomInsetDip();
        var bottomMarginPx = (int)Math.Ceiling((BaseBottomMarginDp + miniBarInset) * density);
        var horizontalMarginPx = (int)(HorizontalMarginDp * density);

        var layoutParams = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Bottom | GravityFlags.CenterHorizontal)
        {
            BottomMargin = bottomMarginPx,
            LeftMargin = horizontalMarginPx,
            RightMargin = horizontalMarginPx
        };

        return layoutParams;
    }

    private static void AnimateIn(View view)
    {
        view.Alpha = 0f;
        view.Animate()?.Alpha(1f)?.SetDuration(FadeAnimationMs)?.Start();
    }

    private static async Task AnimateOutAndRemove(View toastView, FrameLayout rootView)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var tcs = new TaskCompletionSource<bool>();

            toastView.Animate()
                ?.Alpha(0f)
                ?.SetDuration(FadeAnimationMs)
                ?.WithEndAction(new Java.Lang.Runnable(() =>
                {
                    SafeRemoveView(rootView, toastView);
                    tcs.TrySetResult(true);
                }))
                ?.Start();

            return tcs.Task;
        });
    }

    private static void RemoveCurrentToast()
    {
        if (currentToastView == null)
        {
            return;
        }

        try
        {
            currentToastView.Animate()?.Cancel();

            if (currentToastView.Parent is ViewGroup parent)
            {
                parent.RemoveView(currentToastView);
            }
            currentToastView.Dispose();
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error removing previous toast view");
        }

        currentToastView = null;
    }

    private static void SafeRemoveView(FrameLayout rootView, View toastView)
    {
        try
        {
            rootView.RemoveView(toastView);
            toastView.Dispose();

            if (currentToastView == toastView)
            {
                currentToastView = null;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error during toast view cleanup");
        }
    }

    private static Color GetBackgroundColor(AppTheme theme)
    {
        return theme == AppTheme.Dark
            ? new Color(0x2A, 0x2A, 0x2A, 0xF2)
            : new Color(0x00, 0x00, 0x00, 0xCC);
    }

    private static Color GetTextColor()
    {
        return Color.White;
    }

    public override async Task Clear()
    {
        CancelActiveCts();

        await @lock.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RemoveCurrentToast();
                return Task.CompletedTask;
            });
        }
        finally
        {
            @lock.Release();
        }
    }
}
