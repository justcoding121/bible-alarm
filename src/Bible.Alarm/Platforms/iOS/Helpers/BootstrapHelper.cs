using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

public class BootstrapHelper
{
    public static void Initialize(ILogger logger, bool isForeground = false)
    {
        // Initialize database and services (both foreground and background)
        // This must complete before any other tasks
        // Note: DI container initialization is handled by MauiAppHolder.CreateAndStore() at entry points
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
    }

}
