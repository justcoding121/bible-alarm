#nullable enable
using Android.OS;
using Android.Support.V4.Media.Session;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper for presenting a neutral/blank Android Auto UI while the app finishes bootstrap/loading.
/// Intentionally does NOT depend on MAUI DI/services.
/// </summary>
public static class AndroidAutoLoadingUiHelper
{
    public static void ApplyBlankLoadingState(MediaSessionCompat mediaSession, global::Android.Content.Context context)
    {
        // Clear metadata (no artwork, no title/subtitle) + disable all controls.
        // Following standard practice (like YouTube): shows "Tap to play" message without artwork.
        // This avoids "Tap to Open" actions and prevents taps while loading.
        try
        {
            // Clear metadata - no artwork, no text
            mediaSession?.SetMetadata(null);
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
}


