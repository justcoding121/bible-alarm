using System.Runtime.Versioning;
using Android;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.CoordinatorLayout.Widget;
using AndroidX.Core.View;
using AndroidX.Media3.UI;
using Color = Android.Graphics.Color;
using View = Android.Views.View;
using Window = Android.Views.Window;

[assembly: UsesPermission(Manifest.Permission.ForegroundServiceMediaPlayback)]
[assembly: UsesPermission(Manifest.Permission.ForegroundService)]
[assembly: UsesPermission(Manifest.Permission.MediaContentControl)]
[assembly: UsesPermission(Manifest.Permission.PostNotifications)]

namespace CommunityToolkit.Maui.Views;

/// <summary>
/// The user-interface element that represents the <see cref="MediaElement"/> on Android.
/// </summary>
public class MauiMediaElement : CoordinatorLayout
{
    readonly RelativeLayout? relativeLayout;
    readonly PlayerView? playerView;

    int defaultSystemUiVisibility;
    bool isSystemBarVisible;
    bool isFullScreen;

    public MauiMediaElement(nint ptr, JniHandleOwnership _) : base(Platform.AppContext)
    {
        //Fixes no constructor found exception: https://github.com/CommunityToolkit/Maui/pull/1692#issuecomment-1955099758
        // JNI constructor - fields will be initialized in the proper constructor
        relativeLayout = null;
        playerView = null;
        defaultSystemUiVisibility = 0;
        isSystemBarVisible = false;
        isFullScreen = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MauiMediaElement"/> class for headless mode (no UI).
    /// </summary>
    /// <param name="context">The application's <see cref="Context"/>.</param>
    public MauiMediaElement(Context context) : base(context)
    {
        // Headless mode - no PlayerView needed
        relativeLayout = null;
        playerView = null;
        defaultSystemUiVisibility = 0;
        isSystemBarVisible = false;
        isFullScreen = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MauiMediaElement"/> class.
    /// </summary>
    /// <param name="context">The application's <see cref="Context"/>.</param>
    /// <param name="playerView">The <see cref="PlayerView"/> that acts as the platform media player.</param>
    public MauiMediaElement(Context context, PlayerView playerView) : base(context)
    {
        this.playerView = playerView;
        this.playerView.SetBackgroundColor(Color.Black);
        playerView.FullscreenButtonClick += OnFullscreenButtonClick;
        var layout = new RelativeLayout.LayoutParams(LayoutParams.WrapContent, LayoutParams.WrapContent);
        layout.AddRule(LayoutRules.CenterInParent);
        layout.AddRule(LayoutRules.CenterVertical);
        layout.AddRule(LayoutRules.CenterHorizontal);
        relativeLayout = new RelativeLayout(Platform.AppContext)
        {
            LayoutParameters = layout,
        };
        relativeLayout.AddView(playerView);

        AddView(relativeLayout);

        // Initialize fields
        defaultSystemUiVisibility = 0;
        isSystemBarVisible = false;
        isFullScreen = false;
    }

    public override void OnDetachedFromWindow()
    {
        if (isFullScreen)
        {
            OnFullscreenButtonClick(this, new PlayerView.FullscreenButtonClickEventArgs(!isFullScreen));
        }
        base.OnDetachedFromWindow();
    }

    /// <summary>
    /// Checks the visibility of the view
    /// </summary>
    /// <param name="changedView"></param>
    /// <param name="visibility"></param>
    protected override void OnVisibilityChanged(View changedView, [GeneratedEnum] ViewStates visibility)
    {
        base.OnVisibilityChanged(changedView, visibility);
        if (isFullScreen && visibility is ViewStates.Visible)
        {
            SetSystemBarsVisibility();
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="MediaElement"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (playerView?.Player is not null)
                {
                    playerView.Player.PlayWhenReady = false;
                }
                // https://github.com/google/ExoPlayer/issues/1855#issuecomment-251041500
                playerView?.Player?.Release();
                playerView?.Player?.Dispose();
                playerView?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // playerView already disposed
            }
        }

        base.Dispose(disposing);
    }

    void OnFullscreenButtonClick(object? sender, PlayerView.FullscreenButtonClickEventArgs e)
    {
        // Ensure there is a player view
        if (playerView is null || relativeLayout is null)
        {
            throw new InvalidOperationException("PlayerView and RelativeLayout cannot be null when the FullScreen button is tapped");
        }

        // `p0` is the boolean value of isFullScreen being passed into the method. 
        // This is a binding issue that will not be fixed as it is now part of shipped API.
        isFullScreen = e.P0;
        UpdateLayoutForFullscreen(relativeLayout, isFullScreen);
        SetSystemBarsVisibility();
    }

    void UpdateLayoutForFullscreen(RelativeLayout layout, bool enterFullscreen)
    {
        var windowLayout = CurrentPlatformContext.CurrentWindow.DecorView as ViewGroup;

        if (enterFullscreen)
        {
            RemoveView(layout);
            windowLayout?.AddView(layout);
        }
        else
        {
            windowLayout?.RemoveView(layout);
            AddView(layout);
        }
    }

    void SetSystemBarsVisibility()
    {
        var currentWindow = CurrentPlatformContext.CurrentWindow;
        var windowInsetsControllerCompat = WindowCompat.GetInsetsController(currentWindow, currentWindow.DecorView);
        var barTypes = GetBarTypes();

        if (isFullScreen)
        {
            HideSystemBarsForFullscreen(currentWindow, windowInsetsControllerCompat, barTypes);
        }
        else
        {
            ShowSystemBarsForNormalMode(currentWindow, windowInsetsControllerCompat, barTypes);
        }
    }

    static int GetBarTypes()
    {
        return WindowInsetsCompat.Type.StatusBars()
            | WindowInsetsCompat.Type.SystemBars()
            | WindowInsetsCompat.Type.NavigationBars();
    }

    void HideSystemBarsForFullscreen(Window currentWindow, WindowInsetsControllerCompat? windowInsetsControllerCompat, int barTypes)
    {
        // Do not use WindowCompat.SetDecorFitsSystemWindows - deprecated for edge-to-edge (API 35+).
        // On Android 15+ edge-to-edge is default; WindowInsetsController Hide/Show is sufficient.

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            HideSystemBarsAndroid30Plus(currentWindow);
        }
        else
        {
            HideSystemBarsAndroidPre30(currentWindow);
        }

        if (windowInsetsControllerCompat is not null)
        {
            windowInsetsControllerCompat.Hide(barTypes);
            windowInsetsControllerCompat.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }
    }

    void ShowSystemBarsForNormalMode(Window currentWindow, WindowInsetsControllerCompat? windowInsetsControllerCompat, int barTypes)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            ShowSystemBarsAndroid30Plus(currentWindow);
        }
        else
        {
            ShowSystemBarsAndroidPre30(currentWindow);
        }

        if (windowInsetsControllerCompat is not null)
        {
            windowInsetsControllerCompat.Show(barTypes);
            windowInsetsControllerCompat.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorDefault;
        }
    }

    [SupportedOSPlatform("android30.0")]
    void HideSystemBarsAndroid30Plus(Window currentWindow)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            return;
        }

        // Use ViewCompat for API 26+ compatibility
        var windowInsetsCompat = ViewCompat.GetRootWindowInsets(currentWindow.DecorView);
        if (windowInsetsCompat is not null)
        {
            isSystemBarVisible = windowInsetsCompat.IsVisible(WindowInsetsCompat.Type.NavigationBars())
                || windowInsetsCompat.IsVisible(WindowInsetsCompat.Type.StatusBars());

            if (isSystemBarVisible && currentWindow.InsetsController is not null)
            {
                currentWindow.InsetsController.Hide(WindowInsets.Type.SystemBars());
            }
        }
    }

    void HideSystemBarsAndroidPre30(Window currentWindow)
    {
        defaultSystemUiVisibility = (int)currentWindow.DecorView.SystemUiFlags;

        currentWindow.DecorView.SystemUiFlags = currentWindow.DecorView.SystemUiFlags
            | SystemUiFlags.LayoutStable
            | SystemUiFlags.LayoutHideNavigation
            | SystemUiFlags.LayoutFullscreen
            | SystemUiFlags.HideNavigation
            | SystemUiFlags.Fullscreen
            | SystemUiFlags.Immersive;
    }

    [SupportedOSPlatform("android30.0")]
    void ShowSystemBarsAndroid30Plus(Window currentWindow)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            return;
        }

        if (isSystemBarVisible && currentWindow.InsetsController is not null)
        {
            // InsetsController is only available on Android 30+; ShowSystemBarsAndroid30Plus exits early otherwise.
            currentWindow.InsetsController.Show(WindowInsets.Type.SystemBars());
        }
    }

    void ShowSystemBarsAndroidPre30(Window currentWindow)
    {
        currentWindow.DecorView.SystemUiFlags = (SystemUiFlags)defaultSystemUiVisibility;
    }

    readonly record struct CurrentPlatformContext
    {
        public static Activity CurrentActivity
        {
            get
            {
                if (Platform.CurrentActivity is null)
                {
                    throw new InvalidOperationException("CurrentActivity cannot be null");
                }

                return Platform.CurrentActivity;
            }
        }

        public static Window CurrentWindow
        {
            get
            {
                if (CurrentActivity.Window is null)
                {
                    throw new InvalidOperationException("Window cannot be null");
                }

                return CurrentActivity.Window;
            }
        }
    }
}