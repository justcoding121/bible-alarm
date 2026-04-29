#nullable enable
using Android.Graphics;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Service for loading and processing artwork bitmaps for Android platform.
/// Used by Android Auto MediaSession and other Android-specific features.
/// </summary>
public class AndroidArtworkService : IAndroidArtworkService
{
    private static readonly ILogger logger = Log.ForContext<AndroidArtworkService>();

    /// <summary>
    /// Loads artwork bitmap from file path or URI.
    /// Handles file:// URIs and direct file paths.
    /// Scales down large images to recommended size for Android Auto (512x512 max).
    /// </summary>
    public Bitmap? LoadArtworkBitmap(string artworkUrl, int maxSize = 512)
    {
        try
        {
            // Handle file:// URIs
            string filePath = artworkUrl;
            if (artworkUrl.StartsWith(MediaUriSchemeConstants.FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    filePath = new Uri(artworkUrl).LocalPath;
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Could not parse file URI for artwork: {ArtworkUrl}", artworkUrl);
                    return null;
                }
            }

            if (!File.Exists(filePath))
            {
                return null;
            }

            var bitmap = BitmapFactory.DecodeFile(filePath);
            if (bitmap == null)
            {
                return null;
            }

            // Scale down if too large to avoid memory issues (maintain aspect ratio)
            if (bitmap.Width > maxSize || bitmap.Height > maxSize)
            {
                int originalWidth = bitmap.Width;
                int originalHeight = bitmap.Height;
                float scale = Math.Min((float)maxSize / originalWidth, (float)maxSize / originalHeight);
                int scaledWidth = (int)(originalWidth * scale);
                int scaledHeight = (int)(originalHeight * scale);

                var scaledBitmap = Bitmap.CreateScaledBitmap(bitmap, scaledWidth, scaledHeight, true);
                bitmap.Recycle(); // Recycle original bitmap to free memory
                bitmap = scaledBitmap;
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AndroidMediaArtworkLog.ErrorLoadingBitmapFromArtworkUrl, artworkUrl);
            return null;
        }
    }
}

