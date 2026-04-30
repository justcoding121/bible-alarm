#nullable enable

using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Android.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Shared;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Platform;
using Serilog;
using View = Android.Views.View;

namespace Bible.Alarm.Platforms.Android.Services.UI;

/// <summary>
/// Hosts a single MiniPlaybackBar as a native Android view attached to the Activity's
/// content FrameLayout (android.R.id.content). This keeps the bar outside the MAUI
/// NavigationPage hierarchy so it is never recreated during page push/pop transitions.
/// </summary>
public sealed class AndroidMiniPlaybackBarHost : IAndroidMiniPlaybackBarHost,
    IRecipient<ThemeChangedMessage>,
    IDisposable
{
    private static readonly ILogger logger = Log.ForContext<AndroidMiniPlaybackBarHost>();

    private View? nativeBarView;
    private FrameLayout? rootView;
    private bool isDisposed;
    private bool isPlaybackModalActive;

    public AndroidMiniPlaybackBarHost()
    {
        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this);
    }

    public void Attach()
    {
        if (isDisposed)
        {
            return;
        }

        DetachInternal();

        try
        {
            var mauiContext = Application.Current?.Handler?.MauiContext;
            if (mauiContext == null)
            {
                logger.Warning("AndroidMiniPlaybackBarHost: Cannot attach — MauiContext not available");
                return;
            }

            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            rootView = activity?.FindViewById<FrameLayout>(global::Android.Resource.Id.Content);
            if (rootView == null)
            {
                logger.Warning("AndroidMiniPlaybackBarHost: Cannot attach — no content FrameLayout");
                return;
            }

            var miniPlaybackBar = new MiniPlaybackBar();
            nativeBarView = miniPlaybackBar.ToPlatform(mauiContext);

            int navBarBottomPx = GetNavigationBarHeightPx(activity!);

            var layoutParams = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent,
                GravityFlags.Bottom)
            {
                BottomMargin = navBarBottomPx
            };

            rootView.AddView(nativeBarView, layoutParams);

            var vm = MiniPlaybackBarViewModel.Instance;
            if (vm != null)
            {
                vm.PropertyChanged += OnMiniBarViewModelPropertyChanged;
            }

            SyncNativeVisibility();

            logger.Debug("AndroidMiniPlaybackBarHost: Bar attached to Activity content");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "AndroidMiniPlaybackBarHost: Failed to attach bar");
        }
    }

    public void EnsureAttached()
    {
        if (isDisposed)
        {
            return;
        }

        if (IsNativeViewStillValid())
        {
            return;
        }

        logger.Information("AndroidMiniPlaybackBarHost: Native view is stale, re-attaching");
        Attach();
    }

    private bool IsNativeViewStillValid()
    {
        if (nativeBarView == null || rootView == null)
        {
            return false;
        }

        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var currentRoot = activity?.FindViewById<FrameLayout>(global::Android.Resource.Id.Content);

            if (currentRoot == null || currentRoot != rootView)
            {
                return false;
            }

            return nativeBarView.Parent != null;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "AndroidMiniPlaybackBarHost: Error checking native view validity");
            return false;
        }
    }

    public void Detach()
    {
        DetachInternal();
    }

    private void DetachInternal()
    {
        var vm = MiniPlaybackBarViewModel.Instance;
        if (vm != null)
        {
            vm.PropertyChanged -= OnMiniBarViewModelPropertyChanged;
        }

        try
        {
            if (nativeBarView?.Parent != null && rootView != null)
            {
                rootView.RemoveView(nativeBarView);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "AndroidMiniPlaybackBarHost: Error detaching bar");
        }

        nativeBarView = null;
        rootView = null;
    }

    public void SetPlaybackModalActive(bool isActive)
    {
        isPlaybackModalActive = isActive;
        EnsureAttached();
        SyncNativeVisibility();
    }

    /// <summary>
    /// Applies the correct visibility to the native view based on both
    /// <see cref="isPlaybackModalActive"/> and <see cref="MiniPlaybackBarViewModel.IsVisible"/>.
    /// </summary>
    private void SyncNativeVisibility()
    {
        if (nativeBarView == null)
        {
            return;
        }

        try
        {
            var vm = MiniPlaybackBarViewModel.Instance;
            bool shouldBeVisible = !isPlaybackModalActive && vm != null && vm.IsVisible;

            nativeBarView.Visibility = shouldBeVisible
                ? ViewStates.Visible
                : ViewStates.Gone;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "AndroidMiniPlaybackBarHost: Error syncing visibility");
        }
    }

    public void Receive(ThemeChangedMessage message)
    {
        if (isDisposed || nativeBarView == null)
        {
            return;
        }

        // The bar lives outside the MAUI visual tree, so DynamicResource updates
        // from Application.Resources don't propagate. Re-create the bar to pick
        // up the new theme colors.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isDisposed)
            {
                Attach();
            }
        });
    }

    private void OnMiniBarViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MiniPlaybackBarViewModel.IsVisible))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isDisposed)
            {
                EnsureAttached();
                SyncNativeVisibility();
            }
        });
    }

    private static int GetNavigationBarHeightPx(global::Android.App.Activity activity)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var metrics = activity.WindowManager?.CurrentWindowMetrics;
                if (metrics != null)
                {
                    var insets = metrics.WindowInsets.GetInsetsIgnoringVisibility(
                        WindowInsets.Type.NavigationBars());
                    return insets.Bottom;
                }
            }

            var decorView = activity.Window?.DecorView;
            if (decorView != null)
            {
                var rootInsets = ViewCompat.GetRootWindowInsets(decorView);
                var navBars = rootInsets?.GetInsets(WindowInsetsCompat.Type.NavigationBars());
                return navBars?.Bottom ?? 0;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "AndroidMiniPlaybackBarHost: Failed to get navigation bar height");
        }

        return 0;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
        DetachInternal();
    }
}
