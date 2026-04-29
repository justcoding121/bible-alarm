#nullable enable
using Android.Graphics;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;

/// <summary>
/// Handles metadata management for MediaSession.
/// </summary>
public sealed class MetadataManager(ILogger logger, IServiceProvider serviceProvider)
{
    private long? lastDurationMs;

    public long? LastDurationMs
    {
        get => lastDurationMs;
        set => lastDurationMs = value;
    }

    /// <summary>
    /// Creates a metadata builder with title, artist, and album.
    /// </summary>
    public static MediaMetadataCompat.Builder? CreateMetadataBuilder(string title, string artist, string? album)
    {
        // Handle empty strings with fallback values (consistent with AndroidAutoPlayScreenHelper)
        // This ensures we never show empty text in Android Auto UI
        var builder = new MediaMetadataCompat.Builder()
            ?.PutString(MediaMetadataCompat.MetadataKeyTitle, string.IsNullOrEmpty(title) ? "Bible Alarm" : title)
            ?.PutString(MediaMetadataCompat.MetadataKeyArtist, string.IsNullOrEmpty(artist) ? "Tap to play" : artist)
            ?.PutString(MediaMetadataCompat.MetadataKeyAlbum, string.IsNullOrEmpty(album) ? "..." : album);
        // Always include duration key so the time area is always allocated on the Now Playing screen.
        // Prevents layout bounce (title/subtitle shifting) when time appears/disappears during transitions.
        builder?.PutLong(MediaMetadataCompat.MetadataKeyDuration, 0);
        return builder;
    }

    /// <summary>
    /// Preserves existing metadata (MediaId and artwork) in the builder.
    /// </summary>
    public void PreserveExistingMetadata(MediaMetadataCompat.Builder builder, MediaSessionCompat? mediaSession, int? scheduleId, string? artworkUrl)
    {
        if (mediaSession?.Controller?.Metadata != null)
        {
            var existingMetadata = mediaSession.Controller.Metadata;
            PreserveMediaId(builder, existingMetadata, scheduleId);
            PreserveOrLoadArtwork(builder, existingMetadata, artworkUrl);
            PreserveDuration(builder, existingMetadata);
        }
        else
        {
            if (scheduleId.HasValue)
            {
                // Set MediaId if no existing metadata and scheduleId is provided
                builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
            }

            // Load artwork from URL if provided and no existing metadata
            if (builder != null)
            {
                TryLoadArtworkFromUrl(builder, artworkUrl);
            }
        }
    }

    private static void PreserveMediaId(MediaMetadataCompat.Builder builder, MediaMetadataCompat? existingMetadata, int? scheduleId)
    {
        // Preserve MediaId (scheduleId) for OnPlayFromMediaId
        var existingMediaId = existingMetadata?.GetString(MediaMetadataCompat.MetadataKeyMediaId);
        if (!string.IsNullOrEmpty(existingMediaId))
        {
            builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, existingMediaId);
        }
        else if (scheduleId.HasValue)
        {
            // Set MediaId if provided and not already present
            builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
        }
    }

    private static void PreserveDuration(MediaMetadataCompat.Builder? builder, MediaMetadataCompat existingMetadata)
    {
        var existingDuration = existingMetadata.GetLong(MediaMetadataCompat.MetadataKeyDuration);
        if (existingDuration > 0)
        {
            builder?.PutLong(MediaMetadataCompat.MetadataKeyDuration, existingDuration);
        }
    }

    private void PreserveOrLoadArtwork(MediaMetadataCompat.Builder builder, MediaMetadataCompat? existingMetadata, string? artworkUrl)
    {
        if (!string.IsNullOrEmpty(artworkUrl) && TryLoadArtworkFromUrl(builder, artworkUrl))
        {
            return;
        }

        // Preserve existing artwork when no artworkUrl is provided or when loading
        // from artworkUrl failed (file not cached yet).
        // Idle default schedule metadata does not fall back to the app icon; the car UI shows no art instead.
        Bitmap? existingArtwork = existingMetadata?.GetBitmap(MediaMetadataCompat.MetadataKeyArt);
        if (existingArtwork != null)
        {
            builder?.PutBitmap(MediaMetadataCompat.MetadataKeyArt, existingArtwork);
        }
    }

    private bool TryLoadArtworkFromUrl(MediaMetadataCompat.Builder? builder, string? artworkUrl)
    {
        if (string.IsNullOrEmpty(artworkUrl) || builder == null)
        {
            return false;
        }

        try
        {
            var artworkService = serviceProvider.GetService<IAndroidArtworkService>();
            if (artworkService != null)
            {
                var artworkBitmap = artworkService.LoadArtworkBitmap(artworkUrl);
                if (artworkBitmap != null)
                {
                    builder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artworkBitmap);
                    logger.Debug("Loaded artwork bitmap from: {ArtworkUrl}", artworkUrl);
                    return true;
                }

                logger.Debug("Failed to load artwork bitmap from: {ArtworkUrl}", artworkUrl);
            }
            else
            {
                logger.Warning("AndroidArtworkService not available - cannot load artwork");
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", artworkUrl);
        }

        return false;
    }

    /// <summary>
    /// Updates metadata duration if it has changed.
    /// </summary>
    public void UpdateMetadataDuration(MediaSessionCompat? mediaSession, long durationMs)
    {
        // Only update duration if it has changed (duration rarely changes, only on track change)
        // Position updates are frequent (~200ms), but duration only changes when a new track starts
        if (durationMs <= 0 || durationMs == lastDurationMs)
        {
            return;
        }

        if (mediaSession?.Controller?.Metadata == null)
        {
            return;
        }

        // Use CreateMetadataBuilderFromExisting to ensure artwork and all metadata is preserved
        var existingMetadata = mediaSession.Controller.Metadata;
        if (existingMetadata != null)
        {
            var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderFromExisting(existingMetadata);
            metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, durationMs);
            var metadata = metadataBuilder.Build();
            if (metadata != null)
            {
                mediaSession?.SetMetadata(metadata);
                lastDurationMs = durationMs;
                logger.Debug("Duration updated in metadata - Duration: {Duration}ms (artwork preserved)", durationMs);
            }
        }
    }
}
