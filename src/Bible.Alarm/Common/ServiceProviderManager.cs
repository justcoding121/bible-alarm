#nullable enable

namespace Bible.Alarm.Common;

/// <summary>
/// Manages the global service provider for the application.
/// This allows access to services from multiple entry points (direct launch, background service, alarm trigger, etc.)
/// 
/// NOTE: This class now uses MauiAppHolder internally for thread-safe, single-instance MauiApp creation.
/// For new code, prefer using MauiAppHolder directly.
/// </summary>
public static class ServiceProviderManager
{
    /// <summary>
    /// Gets the global service provider. Throws if not initialized.
    /// Now uses MauiAppHolder internally.
    /// </summary>
    public static IServiceProvider ServiceProvider => MauiAppHolder.Services;

    /// <summary>
    /// Initializes the global service provider. Can only be called once.
    /// NOTE: This is now a no-op as MauiAppHolder handles initialization.
    /// Kept for backward compatibility.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use globally</param>
    [Obsolete("ServiceProviderManager now uses MauiAppHolder. Call MauiAppHolder.CreateAndStore() instead.")]
    public static void Initialize(IServiceProvider serviceProvider)
    {
        // No-op - MauiAppHolder handles initialization
        // This method is kept for backward compatibility
    }

    /// <summary>
    /// Gets a service from the global service provider.
    /// Now uses MauiAppHolder internally.
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance</returns>
    public static T GetService<T>() where T : notnull
    {
        return MauiAppHolder.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Safely gets a service from the global service provider, returns null if not initialized.
    /// Now uses MauiAppHolder internally.
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance or null if not initialized</returns>
    public static T? GetServiceSafe<T>() where T : class
    {
        if (!MauiAppHolder.IsInitialized) return null;
        return MauiAppHolder.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service from the global service provider.
    /// Now uses MauiAppHolder internally.
    /// </summary>
    /// <param name="serviceType">The type of service to get</param>
    /// <returns>The service instance</returns>
    public static object GetService(Type serviceType)
    {
        return MauiAppHolder.Services.GetRequiredService(serviceType);
    }

    /// <summary>
    /// Checks if the service provider has been initialized.
    /// Now uses MauiAppHolder internally.
    /// </summary>
    public static bool IsInitialized => MauiAppHolder.IsInitialized;
}