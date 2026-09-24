#nullable enable
using Android.Graphics;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;

/// <summary>
/// Handles metadata management for MediaSession.
/// </summary>
public sealed class MetadataManager(ILogger logger, IServiceProvider serviceProvider)
{
    public long? LastDurationMs { get; set; }

    /// <summary>
    /// Creates a metadata builder with title, artist, and album.
    /// </summary>
    public static MediaMetadataCompat.Builder? CreateMetadataBuilder(string title, string artist, string? album)
    {
        // Handle empty strings with fallback values (consistent with AndroidAutoPlayScreenHelper)
        // Builder is never null; avoid erroneous null-conditioning on fluent chain.
        var builder = new MediaMetadataCompat.Builder();
        builder.PutString(MediaMetadataCompat.MetadataKeyTitle, string.IsNullOrEmpty(title) ? AppConstants.AppSettings.ApplicationDisplayName : title);
        builder.PutString(MediaMetadataCompat.MetadataKeyArtist, string.IsNullOrEmpty(artist) ? AppConstants.Media.NowPlayingPlaceholder.ArtistTapToPlay : artist);
        builder.PutString(MediaMetadataCompat.MetadataKeyAlbum, string.IsNullOrEmpty(album) ? AppConstants.Media.NowPlayingPlaceholder.AlbumEllipsis : album);
        // Always include duration key so the time area is always allocated on the Now Playing screen.
        // Prevents layout bounce (title/subtitle shifting) when time appears/disappears during transitions.
        builder.PutLong(MediaMetadataCompat.MetadataKeyDuration, 0);
        return builder;
    }

    /// <summary>
    /// Keeps artwork and duration for the same schedule, and replaces MediaId when the shown schedule changes.
    /// </summary>
    public void PreserveExistingMetadata(MediaMetadataCompat.Builder builder, MediaSessionCompat? mediaSession, int? scheduleId, string? artworkUrl)
    {
        if (mediaSession?.Controller?.Metadata != null)
        {
            var existingMetadata = mediaSession.Controller.Metadata;
            var existingMediaId = existingMetadata.GetString(MediaMetadataCompat.MetadataKeyMediaId);
            var scheduleChanged = MediaSessionScheduleId.IsScheduleChange(existingMediaId, scheduleId);
            PreserveMediaId(builder, existingMediaId, scheduleId);
            PreserveOrLoadArtwork(builder, existingMetadata, artworkUrl, keepExistingArtwork: !scheduleChanged);
            if (!scheduleChanged)
            {
                PreserveDuration(builder, existingMetadata);
            }
        }
        else
        {
            if (scheduleId.HasValue)
            {
                builder?.PutString(MediaMetadataCompat.MetadataKeyMediaId, scheduleId.Value.ToString());
            }

            if (builder != null)
            {
                TryLoadArtworkFromUrl(builder, artworkUrl);
            }
        }
    }

    private static void PreserveMediaId(MediaMetadataCompat.Builder builder, string? existingMediaId, int? scheduleId)
    {
        var mediaId = MediaSessionScheduleId.Resolve(existingMediaId, scheduleId);
        if (!string.IsNullOrEmpty(mediaId))
        {
            builder.PutString(MediaMetadataCompat.MetadataKeyMediaId, mediaId);
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

    private void PreserveOrLoadArtwork(
        MediaMetadataCompat.Builder builder,
        MediaMetadataCompat? existingMetadata,
        string? artworkUrl,
        bool keepExistingArtwork)
    {
        if (!string.IsNullOrEmpty(artworkUrl) && TryLoadArtworkFromUrl(builder, artworkUrl))
        {
            return;
        }

        // Keep the current cover only while the same schedule stays on screen.
        // A different schedule must not keep the previous cover if its own art is not ready yet.
        if (!keepExistingArtwork)
        {
            return;
        }

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
                    logger.Debug(AppConstants.Logging.AndroidMediaSessionHelperDiagnosticsLog.LoadedArtworkBitmapFromArtworkUrl, artworkUrl);
                    return true;
                }

                logger.Debug(AppConstants.Logging.AndroidMediaArtworkLog.FailedToLoadArtworkBitmapFromArtworkUrl, artworkUrl);
            }
            else
            {
                logger.Warning(AppConstants.Logging.AndroidMediaArtworkLog.AndroidArtworkServiceNotAvailableCannotLoadArtwork);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AndroidMediaArtworkLog.ErrorLoadingBitmapFromArtworkUrl, artworkUrl);
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
        if (durationMs <= 0 || durationMs == LastDurationMs)
        {
            return;
        }

        if (mediaSession?.Controller?.Metadata == null)
        {
            return;
        }

        // Use CreateMetadataBuilderFromExisting to ensure artwork and all metadata is preserved
        var existingMetadata = mediaSession.Controller.Metadata;
        var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderFromExisting(existingMetadata);
        metadataBuilder.PutLong(MediaMetadataCompat.MetadataKeyDuration, durationMs);
        var metadata = metadataBuilder.Build();
        mediaSession!.SetMetadata(metadata);
        LastDurationMs = durationMs;
        logger.Debug(AppConstants.Logging.AndroidMediaArtworkLog.DurationUpdatedInMetadataArtworkPreserved, durationMs);
    }
}
