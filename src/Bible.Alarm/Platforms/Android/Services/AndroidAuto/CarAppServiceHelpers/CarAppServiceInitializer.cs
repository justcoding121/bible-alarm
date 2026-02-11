#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;

/// <summary>
/// Handles initialization for CarAppService.
/// </summary>
public sealed class CarAppServiceInitializer(ILogger logger)
{
    /// <summary>
    /// Initializes the MediaSession for the service.
    /// </summary>
    public void InitializeMediaSession()
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CarAppService.OnCreate: failed to create MediaSession");
        }

        // Create the DI container immediately (fast) so ServiceProviderManager is available synchronously.
        // MediaSession will be created here if needed (for SessionToken), but buffering state is set
        // centrally after bootstrap completes in CommonBootstrapHelper.InitializeSchedules().
        try
        {
            MauiAppHolder.CreateAndStore();
            var mediaSessionManager = ServiceProviderManager.GetService<IMediaSessionManager>();
            mediaSessionManager?.GetOrCreate();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CarAppService.OnCreate: failed to create MauiApp / initialize MediaSession");
        }
    }

    /// <summary>
    /// Initializes bootstrap in the background.
    /// </summary>
    public void InitializeBootstrapInBackground()
    {
        // Ensure MauiApp is created and bootstrap is initialized (idempotent - safe to call multiple times)
        // Bootstrap initialization is thread-safe and will only run once even if called from multiple services
        _ = Task.Run(async () =>
        {
            try
            {
                MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                logger.Information("✅ CarAppService.OnCreate() completed - Bootstrap initialization started");

                // Wait for bootstrap to complete
                // Use a longer timeout for OnCreate since it's not blocking the UI
                // SetCarPlayScreenAction will be dispatched after bootstrap completes (handled by CommonBootstrapHelper)
                try
                {
                    await MauiProgram.WaitForBootstrapAsync();
                }
                catch (Exception bootstrapEx)
                {
                    logger.Warning(bootstrapEx, "Bootstrap timed out in CarAppService.OnCreate - will retry when template is requested");
                    // Don't throw - allow service to continue, template will be generated when bootstrap completes
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing bootstrap in CarAppService");
            }
        });
    }
}
