namespace Bible.Alarm.Common;

/// <summary>
/// Thread-safe holder for the single MauiApp instance.
/// Ensures CreateMauiApp() is called exactly once, regardless of entry point.
/// This is the official Microsoft-recommended pattern for .NET MAUI 9+ apps with multiple entry points.
/// </summary>
public static class MauiAppHolder
{
    private static readonly Lock Lock = new();
    private static MauiApp app;

    public static MauiApp App
    {
        get
        {
            if (app == null)
                throw new InvalidOperationException(
                    "MauiApp has not been created. Call CreateAndStore() first from an entry point.");
            return app;
        }
    }


    public static IServiceProvider Services => App.Services;

    public static MauiApp CreateAndStore(bool isForeground = false)
    {
        lock (Lock)
        {
            if (app != null)
            {
                return app;
            }

            app = MauiProgram.CreateMauiApp();
            return app;
        }
    }

    public static bool IsInitialized => app != null;
}

