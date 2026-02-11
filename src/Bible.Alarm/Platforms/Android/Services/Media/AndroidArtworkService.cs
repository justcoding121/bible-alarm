#nullable enable
using Android.Graphics;
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
            if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    filePath = new Uri(artworkUrl).LocalPath;
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Failed to convert file:// URI to local path: {ArtworkUrl}", artworkUrl);
                    return null;
                }
            }

            // Check if file exists
            if (!File.Exists(filePath))
            {
                logger.Debug("Artwork file does not exist: {FilePath}", filePath);
                return null;
            }

            // Load bitmap from file
            var bitmap = BitmapFactory.DecodeFile(filePath);
            if (bitmap == null)
            {
                logger.Debug("Failed to decode bitmap from file: {FilePath}", filePath);
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
                logger.Debug("Scaled artwork bitmap from {OriginalWidth}x{OriginalHeight} to {ScaledWidth}x{ScaledHeight}",
                    originalWidth, originalHeight, scaledWidth, scaledHeight);
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", artworkUrl);
            return null;
        }
    }
}

