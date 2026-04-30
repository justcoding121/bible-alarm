#nullable enable
using Bible.Alarm.Common.Interfaces.Platform;
using Windows.UI.ViewManagement;

namespace Bible.Alarm.Platforms.Windows.Services.Platform;

/// <summary>
/// Windows implementation of font scale detection using UISettings.TextScaleFactor.
/// This reads the user's text size preference from Windows accessibility settings
/// (Settings > Ease of Access > Display > Make text bigger).
/// </summary>
public sealed partial class WindowsAccessibilityFontScaleService : IAccessibilityFontScaleService, IDisposable
{
    private double currentFontScale;
    private bool isDisposed;
    private readonly UISettings uiSettings;

    public event EventHandler<double>? FontScaleChanged;

    public WindowsAccessibilityFontScaleService()
    {
        uiSettings = new UISettings();

        // Get initial font scale
        currentFontScale = GetSystemFontScale();

        // Listen for text scale factor changes
        uiSettings.TextScaleFactorChanged += OnTextScaleFactorChanged;
    }

    public double FontScale => currentFontScale;

    private double GetSystemFontScale()
    {
        try
        {
            // TextScaleFactor is 1.0 for 100%, can be 1.0-2.25 (100%-225%)
            // This corresponds to Windows Settings > Ease of Access > Display > Make text bigger
            return uiSettings.TextScaleFactor;
        }
        catch (Exception)
        {
            // Silently fall back to default
        }

        return 1.0;
    }

    private void OnTextScaleFactorChanged(UISettings sender, object args)
    {
        var newFontScale = sender.TextScaleFactor;

        // Only notify if scale actually changed
        if (Math.Abs(newFontScale - currentFontScale) > 0.001)
        {
            currentFontScale = newFontScale;

            // Ensure we're on the UI thread for event handlers that update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FontScaleChanged?.Invoke(this, newFontScale);
            });
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        uiSettings.TextScaleFactorChanged -= OnTextScaleFactorChanged;
    }
}
