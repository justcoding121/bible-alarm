#if IOS
using CoreAnimation;
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
        catch (ObjectDisposedException)
        {
            // Native view already deallocated (e.g. after handler disposal) — safe to skip children.
        }

        try
        {
            if (view.Layer != null)
            {
                SuppressFinalizersForLayerHierarchy(view.Layer);
            }
        }
        catch (ObjectDisposedException)
        {
        }

        GC.SuppressFinalize(view);
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
        catch (ObjectDisposedException)
        {
        }

        GC.SuppressFinalize(layer);
    }
}
#endif
