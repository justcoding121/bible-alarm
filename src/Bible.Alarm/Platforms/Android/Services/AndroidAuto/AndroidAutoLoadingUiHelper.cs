#nullable enable
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using AndroidX.Core.Content;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper for presenting a neutral/blank Android Auto UI while the app finishes bootstrap/loading.
/// Intentionally does NOT depend on MAUI DI/services.
/// </summary>
public static class AndroidAutoLoadingUiHelper
{
    public static void ApplyBlankLoadingState(MediaSessionCompat mediaSession, global::Android.Content.Context context)
    {
        // Show only artwork (no title/subtitle) + disable all controls.
        // This avoids "Tap to Open" actions and prevents taps while loading.
        try
        {
            var drawable = ContextCompat.GetDrawable(context, Resource.Mipmap.ic_launcher_round);
            var bitmap = DrawableToBitmap(drawable);
            if (bitmap != null)
            {
                var metadataBuilder = new MediaMetadataCompat.Builder()
                    .PutBitmap(MediaMetadataCompat.MetadataKeyArt, bitmap);
                mediaSession?.SetMetadata(metadataBuilder?.Build());
            }
            else
            {
                mediaSession?.SetMetadata(null);
            }
        }
        catch
        {
            mediaSession?.SetMetadata(null);
        }

        var builder = new PlaybackStateCompat.Builder();
        if (builder != null)
        {
            builder.SetActions(0);
            builder.SetState(PlaybackStateCompat.StateNone, 0, 0.0f, SystemClock.ElapsedRealtime());
            var playbackState = builder.Build();
            if (playbackState != null)
            {
                mediaSession?.SetPlaybackState(playbackState);
            }
        }
        if (mediaSession != null)
        {
            mediaSession.Active = false;
        }
    }

    private static Bitmap? DrawableToBitmap(Drawable? drawable)
    {
        if (drawable == null)
        {
            return null;
        }

        if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
        {
            return bitmapDrawable.Bitmap;
        }

        var width = drawable.IntrinsicWidth > 0 ? drawable.IntrinsicWidth : 256;
        var height = drawable.IntrinsicHeight > 0 ? drawable.IntrinsicHeight : 256;

        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888 ?? Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
        drawable.Draw(canvas);
        return bitmap;
    }
}


