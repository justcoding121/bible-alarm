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
    /// Gets a service from the global service provider.
    /// Now uses MauiAppHolder internally.
    /// </summary>
    public static T GetService<T>() where T : notnull
    {
        return MauiAppHolder.Services.GetRequiredService<T>();
    }

}
