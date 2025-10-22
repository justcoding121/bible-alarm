using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Tasks;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

public class BootstrapHelper
{
    public static void Initialize(ILogger logger)
    {
        // Ensure ServiceProviderManager is initialized for iOS
        EnsureServiceProviderInitialized();
        
        Task.Run(async () =>
        {
            try
            {
                await CommonBootstrapHelper.VerifyServices();
                Messenger<bool>.Publish(MvvmMessages.Initialized, true);

                await Task.Delay(1000);

                try
                {
                    using var schedulerTask = ServiceProviderManager.GetService<SchedulerTask>();
                    var downloaded = await schedulerTask.Handle();
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error occurred in cleanup task.");
                }
            }
            catch (Exception e)
            {
                logger.Fatal(e, "iOS initialization crashed.");
            }
        });
    }

    public static async Task VerifyServices()
    {
        // Ensure ServiceProviderManager is initialized
        EnsureServiceProviderInitialized();
        await CommonBootstrapHelper.VerifyServices();
    }

    private static void EnsureServiceProviderInitialized()
    {
        if (!ServiceProviderManager.IsInitialized)
        {
            // Initialize minimal DI container for iOS background services
            // This should only happen if the main app hasn't started yet
            throw new InvalidOperationException(
                "ServiceProviderManager not initialized. iOS background services require the main app to initialize first.");
        }
    }
}
