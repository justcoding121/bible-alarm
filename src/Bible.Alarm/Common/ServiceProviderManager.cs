namespace Bible.Alarm;

/// <summary>
/// Manages the global service provider for the application.
/// This allows access to services from multiple entry points (direct launch, background service, alarm trigger, etc.)
/// </summary>
public static class ServiceProviderManager
{
    private static IServiceProvider _serviceProvider;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the global service provider. Throws if not initialized.
    /// </summary>
    public static IServiceProvider ServiceProvider
    {
        get
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException(
                    "Service provider has not been initialized. Call Initialize() first.");
            return _serviceProvider;
        }
    }

    /// <summary>
    /// Initializes the global service provider. Can only be called once.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use globally</param>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        lock (_lock)
        {
            if (_serviceProvider != null)
                throw new InvalidOperationException("Service provider has already been initialized.");
            _serviceProvider = serviceProvider;
        }
    }

    /// <summary>
    /// Gets a service from the global service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance</returns>
    public static T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service from the global service provider.
    /// </summary>
    /// <param name="serviceType">The type of service to get</param>
    /// <returns>The service instance</returns>
    public static object GetService(Type serviceType)
    {
        return ServiceProvider.GetRequiredService(serviceType);
    }

    /// <summary>
    /// Checks if the service provider has been initialized.
    /// </summary>
    public static bool IsInitialized => _serviceProvider != null;
}