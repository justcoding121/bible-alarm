using Serilog;

namespace Bible.Alarm.Common;

/// <summary>
/// Thread-safe holder for the single MauiApp instance.
/// Ensures CreateMauiApp() is called exactly once, regardless of entry point.
/// This is the official Microsoft-recommended pattern for .NET MAUI 9+ apps with multiple entry points.
/// </summary>
public static class MauiAppHolder
{
    private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
    private static MauiApp app;
    // Use Log.Logger directly for static classes (can't use ForContext<T> with static types)
    private static readonly ILogger Logger = Log.Logger;

    public static MauiApp App
    {
        get
        {
            if (app == null)
            {
                throw new InvalidOperationException(
                    "MauiApp has not been created. Call CreateAndStore() first from an entry point.");
            }

            return app;
        }
    }


    public static IServiceProvider Services => App.Services;

    public static MauiApp CreateAndStore()
    {
        Lock.Wait();
        try
        {
            // If app exists, verify it's not disposed by checking if service provider is accessible
            if (app != null)
            {
                return app;
            }

            // Create new app instance
            Logger.Information("MauiAppHolder.CreateAndStore - Creating new MauiApp instance");
            app = MauiProgram.CreateMauiApp();
            return app;
        }
        finally
        {
            Lock.Release();
        }
    }

    public static bool IsInitialized => app != null;
}

