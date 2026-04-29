#nullable enable
using Android.Support.V4.Media.Session;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;

/// <summary>
/// Handles MediaSession initialization and setup for LegacyMediaBrowserService.
/// </summary>
public sealed class MediaSessionInitializer(ILogger logger)
{
    private MediaSessionCompat? session;
    private IMediaSessionManager? mediaSessionManager;

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
            logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.ErrorInitializingMediaSessionWillRetryWhenBound);
        }
    }

    /// <summary>
    /// Sets up the MediaSession manager and token.
    /// </summary>
    private void SetupMediaSessionManagerAndToken()
    {
        // MediaSession is created here if needed (for SessionToken), but buffering state is set
        // centrally after bootstrap completes in CommonBootstrapHelper.InitializeSchedules().
        mediaSessionManager = ServiceProviderManager.GetService<IMediaSessionManager>();
        if (mediaSessionManager == null)
        {
            logger.Warning(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.MediaSessionManagerNullCannotCreateMediaSession);
            return;
        }

        session = mediaSessionManager.GetOrCreate();
        if (session == null)
        {
            logger.Error(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.MediaSessionCompatNullAfterGetOrCreateCannotSetSessionToken);
            return;
        }

        if (session.SessionToken == null)
        {
            logger.Error(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.MediaSessionCompatSessionTokenNullMayNotBeInitialized);
            return;
        }

        logger.Information(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.SessionTokenSuccessfullySet, session.SessionToken?.ToString() ?? "null");
        logger.Information(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.OnCreateCompletedLegacyAaConnectingSessionTokenOk);
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
                logger.Information(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.OnCreateCompletedBootstrapInitializationStarted);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // State subscription will be handled by StateSubscriptionManager
                        logger.Information(AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.BootstrapInitializationCompletedForLegacyMediaBrowser);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.ErrorInBootstrapInitialization);
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.LegacyMediaBrowserMediaSessionInitializerDiagnosticsLog.ErrorInitializingBootstrapInLegacyMediaBrowser);
            }
        });
    }
}
