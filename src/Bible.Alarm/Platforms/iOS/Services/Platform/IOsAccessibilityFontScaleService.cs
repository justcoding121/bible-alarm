#nullable enable

#pragma warning disable S101 // IOs prefix marks iOS platform implementations.

using Bible.Alarm.Common.Interfaces.Platform;
using Foundation;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Platform;

/// <summary>
/// iOS implementation of font scale detection using UIApplication.SharedApplication.PreferredContentSizeCategory.
/// This reads the user's Dynamic Type font size preference from iOS accessibility settings.
/// </summary>
public sealed class IOsAccessibilityFontScaleService : IAccessibilityFontScaleService, IDisposable
{
    private double currentFontScale;
    private bool isDisposed;
    private NSObject? contentSizeCategoryObserver;

    public event EventHandler<double>? FontScaleChanged;

    public IOsAccessibilityFontScaleService()
    {
        // Get initial font scale
        currentFontScale = GetSystemFontScale();

        // Listen for Dynamic Type changes
        contentSizeCategoryObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.ContentSizeCategoryChangedNotification,
            OnContentSizeCategoryChanged);
    }

    public double FontScale => currentFontScale;

    private static double GetSystemFontScale()
    {
        try
        {
            var category = UIApplication.SharedApplication.PreferredContentSizeCategory;
            return ContentSizeCategoryToScale(category);
        }
        catch (Exception)
        {
            // Silently fall back to default
        }

        return 1.0;
    }

    /// <summary>
    /// Converts iOS content size category to a numeric scale factor.
    /// These values approximate the font size multipliers iOS uses for Dynamic Type.
    /// </summary>
    private static double ContentSizeCategoryToScale(string category)
    {
        // Map content size categories to approximate scale factors
        // Based on Apple's Dynamic Type guidelines
        return category switch
        {
            // Accessibility sizes (require "Larger Accessibility Sizes" enabled)
            "UICTContentSizeCategoryAccessibilityExtraExtraExtraLarge" => 3.12,
            "UICTContentSizeCategoryAccessibilityExtraExtraLarge" => 2.76,
            "UICTContentSizeCategoryAccessibilityExtraLarge" => 2.35,
            "UICTContentSizeCategoryAccessibilityLarge" => 1.95,
            "UICTContentSizeCategoryAccessibilityMedium" => 1.64,

            // Standard sizes
            "UICTContentSizeCategoryExtraExtraExtraLarge" => 1.35,
            "UICTContentSizeCategoryExtraExtraLarge" => 1.24,
            "UICTContentSizeCategoryExtraLarge" => 1.12,
            "UICTContentSizeCategoryLarge" => 1.0,        // Default
            "UICTContentSizeCategoryMedium" => 0.94,
            "UICTContentSizeCategorySmall" => 0.88,
            "UICTContentSizeCategoryExtraSmall" => 0.82,

            _ => 1.0 // Unknown category, use default
        };
    }

    private void OnContentSizeCategoryChanged(NSNotification notification)
    {
        var newFontScale = GetSystemFontScale();

        // Only notify if scale actually changed
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

        if (contentSizeCategoryObserver != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(contentSizeCategoryObserver);
            contentSizeCategoryObserver.Dispose();
            contentSizeCategoryObserver = null;
        }
    }
}
