#nullable enable
#if IOS
using Bible.Alarm.Shared.Constants;
using CoreAnimation;
using Foundation;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Best-effort walk of UIView/CALayer hierarchies during teardown (logs if native objects are already disposed).
/// </summary>
public static class IosNativeViewCleanupHelper
{
    public static void SuppressFinalizersForViewHierarchy(UIView view)
    {
        try
        {
            var subviews = view.Subviews;
            if (subviews != null)
            {
                foreach (var subview in subviews)
                {
                    SuppressFinalizersForViewHierarchy(subview);
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, AppConstants.Logging.IosNativeViewCleanupDiagnosticsLog.SubviewsWalkHitDisposedViewNonFatal);
        }

        try
        {
            if (view.Layer != null)
            {
                SuppressFinalizersForLayerHierarchy(view.Layer);
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, AppConstants.Logging.IosNativeViewCleanupDiagnosticsLog.LayerAccessDisposedNonFatal);
        }

        SuppressViewPropertyFinalizers(view);
    }

    /// <summary>
    /// Visits common NSObject-derived properties on UIView subclasses
    /// (e.g. UIImage on UIImageView, UIColor on views) that can outlive their native peers.
    /// </summary>
    private static void SuppressViewPropertyFinalizers(UIView view)
    {
        try
        {
            if (view is UIImageView imageView)
            {
                SuppressFinalizer(imageView.Image);
                SuppressFinalizer(imageView.HighlightedImage);
            }
            else if (view is UIButton button)
            {
                SuppressFinalizer(button.CurrentImage);
                SuppressFinalizer(button.CurrentBackgroundImage);
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, AppConstants.Logging.IosNativeViewCleanupDiagnosticsLog.PropertyFinalizerWalkDisposedNonFatal);
        }
    }

    public static void SuppressFinalizersForLayerHierarchy(CALayer layer)
    {
        try
        {
            var sublayers = layer.Sublayers;
            if (sublayers != null)
            {
                foreach (var sublayer in sublayers)
                {
                    SuppressFinalizersForLayerHierarchy(sublayer);
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, AppConstants.Logging.IosNativeViewCleanupDiagnosticsLog.SublayersWalkDisposedNonFatal);
        }
    }

    /// <summary>
    /// Retained as a structured no-op visitor for NSObject properties (keeps call sites stable).
    /// </summary>
    public static void SuppressFinalizer(NSObject? obj) => _ = obj;
}
#endif
