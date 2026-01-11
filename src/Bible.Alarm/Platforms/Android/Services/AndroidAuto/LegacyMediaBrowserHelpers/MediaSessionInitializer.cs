#nullable enable
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;

/// <summary>
/// Handles MediaSession initialization and setup for LegacyMediaBrowserService.
/// </summary>
public sealed class MediaSessionInitializer(ILogger logger)
{
    private MediaSessionCompat? session;
    private Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManager? mediaSessionManager;

    /// <summary>
    /// Initializes the MediaSession for the service.
    /// </summary>
    public void InitializeMediaSession()
    {
        try
        {
            MauiAppHolder.CreateAndStore();
            SetupMediaSessionManagerAndToken();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing MediaSession in LegacyMediaBrowserService - will retry when service is bound");
        }
    }

    /// <summary>
    /// Sets up the MediaSession manager and token.
    /// </summary>
    private void SetupMediaSessionManagerAndToken()
    {
        // MediaSession is created here if needed (for SessionToken), but buffering state is set
        // centrally after bootstrap completes in CommonBootstrapHelper.InitializeSchedules().
        mediaSessionManager = ServiceProviderManager.GetService<Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManager>();
        if (mediaSessionManager == null)
        {
            logger.Warning("MediaSessionManager is null - cannot create MediaSession");
            return;
        }

        session = mediaSessionManager.GetOrCreate();
        if (session == null)
        {
            logger.Error("MediaSessionCompat is null after GetOrCreate() - cannot set SessionToken");
            return;
        }

        if (session.SessionToken == null)
        {
            logger.Error("MediaSessionCompat.SessionToken is null - MediaSessionCompat may not be properly initialized");
            return;
        }

        logger.Information("SessionToken successfully set: {Token}", session.SessionToken?.ToString() ?? "null");
        logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Legacy Android Auto is connecting! SessionToken set correctly.");
    }

    /// <summary>
    /// Gets the MediaSession instance.
    /// </summary>
    public MediaSessionCompat? Session => session;

    /// <summary>
    /// Initializes bootstrap in the background.
    /// </summary>
    public void InitializeBootstrapInBackground()
    {
        _ = Task.Run(() =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                logger.Information("✅ LegacyMediaBrowserService.OnCreate() completed - Bootstrap initialization started");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // State subscription will be handled by StateSubscriptionManager
                        logger.Information("Bootstrap initialization completed for LegacyMediaBrowserService");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in bootstrap initialization");
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing bootstrap in LegacyMediaBrowserService");
            }
        });
    }
}
