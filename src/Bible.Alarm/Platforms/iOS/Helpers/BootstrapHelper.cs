using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Scheduler;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

public class BootstrapHelper
{
    public static void Initialize(ILogger logger, bool isForeground = false)
    {
        // Ensure ServiceProviderManager is initialized for iOS
        EnsureServiceProviderInitialized();
        
        // Initialize database and services (both foreground and background)
        // This must complete before any other tasks
        try
        {
            CommonBootstrapHelper.VerifyServices().Wait();
            logger.Information("iOS database initialization completed successfully.");
        }
        catch (Exception e)
        {
            logger.Fatal(e, "iOS database initialization crashed.");
            throw;
        }
        
        // Initialize UI components only for foreground scenarios
        if (isForeground)
        {
            InitializeUi(logger);
        }
    }

    private static void InitializeUi(ILogger logger)
    {
        Task.Run(async () =>
        {
            try
            {
                // UI-specific initialization only
                WeakReferenceMessenger.Default.Send(new InitializedMessage(true));

                await Task.Delay(1000);

                try
                {
                    using var schedulerService = ServiceProviderManager.GetService<SchedulerService>();
                    var downloaded = await schedulerService.Handle();
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error occurred in cleanup task.");
                }
            }
            catch (Exception e)
            {
                logger.Fatal(e, "iOS UI initialization crashed.");
            }
        });
    }


    private static void EnsureServiceProviderInitialized()
    {
        if (!ServiceProviderManager.IsInitialized)
        {
            // Initialize DI container for background services
            var logger = Log.ForContext<BootstrapHelper>();
            logger.Information("Initializing DI container for background service...");
            
            // Call MauiProgram to ensure DI container is initialized
            MauiProgram.EnsureDiContainerInitialized();
            
            logger.Information("DI container initialized for background service.");
        }
    }
}
