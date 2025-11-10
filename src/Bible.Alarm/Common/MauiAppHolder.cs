#nullable enable

namespace Bible.Alarm.Common;

/// <summary>
/// Thread-safe holder for the single MauiApp instance.
/// Ensures CreateMauiApp() is called exactly once, regardless of entry point.
/// This is the official Microsoft-recommended pattern for .NET MAUI 9+ apps with multiple entry points.
/// </summary>
public static class MauiAppHolder
{
    private static readonly object Lock = new();
    private static MauiApp? _app;

    /// <summary>
    /// Gets the MauiApp instance. Throws if not created yet.
    /// </summary>
    public static MauiApp App
    {
        get
        {
            if (_app == null)
                throw new InvalidOperationException(
                    "MauiApp has not been created. Call CreateAndStore() first from an entry point.");
            return _app;
        }
    }

    /// <summary>
    /// Gets the IServiceProvider from the MauiApp. Throws if not created yet.
    /// </summary>
    public static IServiceProvider Services => App.Services;

    /// <summary>
    /// Creates and stores the MauiApp instance exactly once.
    /// Thread-safe and can be called from any entry point (MainApplication, AppDelegate, background service, etc.)
    /// </summary>
    /// <returns>The MauiApp instance (existing if already created, or newly created)</returns>
    public static MauiApp CreateAndStore()
    {
        lock (Lock)
        {
            if (_app != null)
            {
                // Already created → return existing
                return _app;
            }

            // Create new MauiApp instance
            _app = MauiProgram.CreateMauiApp();
            return _app;
        }
    }

    /// <summary>
    /// Checks if the MauiApp has been created.
    /// </summary>
    public static bool IsInitialized => _app != null;
}

