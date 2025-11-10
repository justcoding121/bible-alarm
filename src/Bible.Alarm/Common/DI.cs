#nullable enable

namespace Bible.Alarm.Common;

/// <summary>
/// Convenience helper for accessing services from the global DI container.
/// Uses MauiAppHolder internally for thread-safe access.
/// </summary>
public static class DI
{
    /// <summary>
    /// Gets a service from the global service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if MauiApp has not been initialized</exception>
    public static T Get<T>() where T : notnull
    {
        return MauiAppHolder.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Safely gets a service from the global service provider, returns null if not initialized.
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance or null if MauiApp has not been initialized</returns>
    public static T? GetSafe<T>() where T : class
    {
        if (!MauiAppHolder.IsInitialized) return null;
        return MauiAppHolder.Services.GetRequiredService<T>();
    }
}

