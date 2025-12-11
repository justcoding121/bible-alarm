using Bible.Alarm.Common.Helpers;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

public class iOSBootstrapHelper
{
    public static async Task Initialize(ILogger logger, bool isForeground = false)
    {
        try
        {
            // Pass isForeground to VerifyServices so InitializedMessage is sent for foreground launches
            // Database operations run in background Task.Run, so UI thread is not blocked
            await CommonBootstrapHelper.VerifyServices(isForeground);
            logger.Information("iOS database initialization completed successfully.");
        }
        catch (Exception e)
        {
            logger.Fatal(e, "iOS database initialization crashed.");
            throw;
        }
    }

}
