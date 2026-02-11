#nullable enable
using Android.Graphics;

namespace Bible.Alarm.Platforms.Android.Services.Media.Interfaces;

/// <summary>
/// Service for loading and processing artwork bitmaps for Android platform.
/// </summary>
public interface IAndroidArtworkService
{
    /// <summary>
    /// Loads artwork bitmap from file path or URI.
    /// </summary>
    Bitmap? LoadArtworkBitmap(string artworkUrl, int maxSize = 512);
}
