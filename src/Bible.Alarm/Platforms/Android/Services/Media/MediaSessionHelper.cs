#nullable enable
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Serilog;
using Application = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Global static helper for creating the shared MediaSessionCompat instance.
/// Uses locking to prevent duplicate creation across multiple entry points.
/// This ensures MediaSession is created as early as possible, even in background processes.
/// </summary>
public static class MediaSessionHelper
{
    private static MediaSessionCompat? mediaSession;
    private static readonly Lock @lock = new();
    private static readonly ILogger logger = Log.ForContext(typeof(MediaSessionHelper));

    /// <summary>
    /// Creates the shared MediaSessionCompat instance with thread-safe locking.
    /// Returns the existing instance if already created.
    /// Uses Android Application context to work in all entry points (foreground, background, services).
    /// </summary>
    /// <returns>The shared MediaSessionCompat instance</returns>
    public static MediaSessionCompat Create()
    {
        // Fast path: if already created, return it
        if (mediaSession == null)
        {
            // Double-checked locking pattern for thread safety
            lock (@lock)
            {
                // Check again inside lock (another thread might have created it)
                if (mediaSession == null)
                {
                    var context = GetApplicationContext();
                    mediaSession = InitializeMediaSession(context);
                    ApplyInitialLoadingState(mediaSession);
                    VerifySessionToken(mediaSession);

                    // Set last played metadata to MediaSession if available (before DI initialization)
                    // This ensures Android Auto shows the last played item immediately on process start
                    ApplyLastPlayedMetadataIfAvailable(mediaSession, context);

                    logger.Information("MediaSessionCompat created successfully via MediaSessionHelper. Initial state: Buffering, Active: True, SessionToken available: {HasToken}",
                        mediaSession.SessionToken != null);
                }
            }
        }

        return mediaSession;
    }

    private static Context GetApplicationContext() => Application.Context;

    private static MediaSessionCompat InitializeMediaSession(Context context)
    {
        logger.Information("Creating shared MediaSessionCompat instance (2025 Standard)");

        // 2025 NON-DEPRECATED CONSTRUCTOR: Only context and tag needed
        // Note: Ensure your AndroidManifest.xml has a MediaButtonReceiver registered
        // The system now automatically finds your MediaButtonReceiver via the <intent-filter> in AndroidManifest.xml
        var session = new MediaSessionCompat(context, "BibleAlarmSession");

        // REMOVED SetFlags: FlagHandlesMediaButtons and FlagHandlesTransportControls are now default behavior in 2025

        // CRITICAL: Set Active = true for Android Auto to "see" the session
        session.Active = true;

        return session;
    }

    private static void ApplyInitialLoadingState(MediaSessionCompat session)
    {
        // Set initial buffering state when MediaSession is first created (BEFORE bootstrap).
        // This is called automatically when Create() creates a new MediaSession.
        // After bootstrap completes, DefaultScheduleService will set metadata via SetDefaultScheduleMetadataAction,
        // so this blank loading state is only used during the initial process startup phase.
        try
        {
            AndroidAutoPlayScreenHelper.ApplyBlankLoadingState(session);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to apply Android Auto buffering state");
        }
    }

    private static void VerifySessionToken(MediaSessionCompat session)
    {
        // Verify SessionToken is available
        if (session.SessionToken == null)
        {
            logger.Error("MediaSessionCompat.SessionToken is null after creation - this should not happen");
            throw new InvalidOperationException("MediaSessionCompat.SessionToken is null after creation");
        }
    }

    /// <summary>
    /// Applies last played metadata to MediaSession if available from Preferences.
    /// If Preferences metadata is not available, sets blank loading state.
    /// This happens before DI initialization, so we set metadata directly on MediaSessionCompat.
    /// Similar to how default schedule metadata is set, but using saved Preferences data.
    /// </summary>
    private static void ApplyLastPlayedMetadataIfAvailable(MediaSessionCompat session, Context context)
    {
        try
        {
            var metadata = LastPlayedMetadataHelper.GetAllPreferenceMetadata();
            if (metadata == null)
            {
                logger.Debug("No last played metadata found in Preferences - already in blank loading state");
                // Don't call ApplyBlankLoadingState again - it was already called in ApplyInitialLoadingState
                return;
            }

            logger.Information("Applying last played metadata to MediaSession - Title: {Title}, Artist: {Artist}, ScheduleId: {ScheduleId}",
                metadata.Value.Title, metadata.Value.Artist, metadata.Value.ScheduleId);

            // Build metadata directly (no DI dependencies)
            var metadataBuilder = AndroidAutoPlayScreenHelper.CreateMetadataBuilderWithMediaId(
                metadata.Value.Title,
                metadata.Value.Artist,
                metadata.Value.Album,
                metadata.Value.ScheduleId);

            // Try to load artwork if URL is available (simple file-based loading without DI)
            if (!string.IsNullOrEmpty(metadata.Value.ArtworkUrl))
            {
                LoadArtworkForMetadata(metadataBuilder, metadata.Value.ArtworkUrl, context);
            }

            // Apply metadata to session
            var builtMetadata = metadataBuilder.Build();
            session.SetMetadata(builtMetadata);

            // Set to stopped state (idle, ready to play) - similar to default schedule
            AndroidAutoPlayScreenHelper.SetStoppedState(session);

            logger.Debug("Successfully applied last played metadata to MediaSession");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to apply last played metadata to MediaSession");
        }
    }

    /// <summary>
    /// Loads artwork bitmap from URL and adds it to metadata builder.
    /// Uses simple file-based loading without DI dependencies.
    /// </summary>
    private static void LoadArtworkForMetadata(MediaMetadataCompat.Builder metadataBuilder, string artworkUrl, Context context)
    {
        try
        {
            Bitmap? artworkBitmap = null;

            // Try to load from file path if it's a local file
            if (System.IO.File.Exists(artworkUrl))
            {
                artworkBitmap = BitmapFactory.DecodeFile(artworkUrl);
            }
            else if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // Handle file:// URLs
                var filePath = artworkUrl.Replace("file://", "");
                if (System.IO.File.Exists(filePath))
                {
                    artworkBitmap = BitmapFactory.DecodeFile(filePath);
                }
            }

            if (artworkBitmap != null)
            {
                metadataBuilder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artworkBitmap);
                logger.Debug("Loaded artwork bitmap from: {ArtworkUrl}", artworkUrl);
            }
            else
            {
                logger.Debug("Could not load artwork from: {ArtworkUrl} (file not found or invalid format)", artworkUrl);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error loading artwork bitmap from: {ArtworkUrl}", artworkUrl);
        }
    }
}

