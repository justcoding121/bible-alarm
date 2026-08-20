#nullable enable
using Android.Content;
using Android.Content.Res;
using Bible.Alarm.Common.Interfaces.Platform;
using AndroidApplication = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Platform;

/// <summary>
/// Android implementation of font scale detection using Configuration.fontScale.
/// This reads the user's font size preference from Android accessibility settings.
/// </summary>
public sealed class AndroidAccessibilityFontScaleService : IAccessibilityFontScaleService, IDisposable
{
    private double currentFontScale;
    private bool isDisposed;
    private readonly FontScaleComponentCallbacks componentCallbacks;

    public event EventHandler<double>? FontScaleChanged;

    public AndroidAccessibilityFontScaleService()
    {
        currentFontScale = GetSystemFontScale();

        // Listen for configuration changes (includes font scale changes)
        componentCallbacks = new FontScaleComponentCallbacks(OnConfigurationChanged);
        AndroidApplication.Context.RegisterComponentCallbacks(componentCallbacks);
    }

    public double FontScale => currentFontScale;

    private static double GetSystemFontScale()
    {
        try
        {
            var resources = AndroidApplication.Context.Resources;
            if (resources?.Configuration != null)
            {
                // fontScale is 1.0 for normal, can be 0.85-2.0+ depending on user preference
                return resources.Configuration.FontScale;
            }
        }
        catch (Exception)
        {
            // Silently fall back to default
        }

        return 1.0;
    }

    private void OnConfigurationChanged(Configuration newConfig)
    {
        var newFontScale = newConfig.FontScale;

        // Only notify if scale actually changed (avoid double notifications)
        if (Math.Abs(newFontScale - currentFontScale) > 0.001)
        {
            currentFontScale = newFontScale;
            FontScaleChanged?.Invoke(this, newFontScale);
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        try
        {
            AndroidApplication.Context.UnregisterComponentCallbacks(componentCallbacks);
        }
        catch (Exception)
        {
            // Ignore disposal errors
        }
    }

    /// <summary>
    /// Custom ComponentCallbacks2 implementation to listen for configuration changes.
    /// Uses IComponentCallbacks2 interface for .NET 10+ Android compatibility.
    /// </summary>
    private sealed class FontScaleComponentCallbacks : Java.Lang.Object, IComponentCallbacks2
    {
        private readonly Action<Configuration> onConfigurationChanged;

        public FontScaleComponentCallbacks(Action<Configuration> onConfigurationChanged)
        {
            this.onConfigurationChanged = onConfigurationChanged;
        }

        public void OnConfigurationChanged(Configuration newConfig)
        {
            onConfigurationChanged?.Invoke(newConfig);
        }

        public void OnLowMemory()
        {
            // Not used for font scale detection
        }

        public void OnTrimMemory(global::Android.Content.TrimMemory level)
        {
            // Not used for font scale detection
        }
    }
}
