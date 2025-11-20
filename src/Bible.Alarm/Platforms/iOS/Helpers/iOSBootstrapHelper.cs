using Bible.Alarm.Common.Helpers;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Helpers;

public class iOSBootstrapHelper
{
    public static void Initialize(ILogger logger, bool isForeground = false)
    {
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
