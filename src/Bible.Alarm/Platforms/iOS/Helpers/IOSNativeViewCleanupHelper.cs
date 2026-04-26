#nullable enable
#if IOS
using CoreAnimation;
using Foundation;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Helpers;

/// <summary>
/// Suppresses GC finalization for UIView/CALayer hierarchies to prevent
/// SIGSEGV crashes when the native side deallocates objects before the
/// managed finalizer runs (e.g. StaticCAShapeLayer sending objc_msgSend
/// to an already-deallocated native peer).
/// </summary>
public static class IOSNativeViewCleanupHelper
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
            Log.Debug(ex, "IOSNativeViewCleanupHelper: Subviews walk hit disposed view (non-fatal)");
        }

        try
        {
            var gestureRecognizers = view.GestureRecognizers;
            if (gestureRecognizers != null)
            {
                foreach (var recognizer in gestureRecognizers)
                {
                    GC.SuppressFinalize(recognizer);
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, "IOSNativeViewCleanupHelper: GestureRecognizers access disposed (non-fatal)");
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
            Log.Debug(ex, "IOSNativeViewCleanupHelper: Layer access disposed (non-fatal)");
        }

        SuppressViewPropertyFinalizers(view);

        GC.SuppressFinalize(view);
    }

    /// <summary>
    /// Suppresses finalizers for common NSObject-derived properties on UIView subclasses
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
            Log.Debug(ex, "IOSNativeViewCleanupHelper: Property finalizer walk disposed (non-fatal)");
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
            Log.Debug(ex, "IOSNativeViewCleanupHelper: Sublayers walk disposed (non-fatal)");
        }

        GC.SuppressFinalize(layer);
    }

    /// <summary>
    /// Suppresses finalizers on an NSObject that has already been disposed,
    /// preventing the GC finalizer from sending objc_msgSend to freed native objects.
    /// Safe to call with null or already-disposed objects.
    /// </summary>
    public static void SuppressFinalizer(NSObject? obj)
    {
        if (obj != null)
        {
            GC.SuppressFinalize(obj);
        }
    }
}
#endif
