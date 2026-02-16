using Bible.Alarm.VersionPatcher.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVersionPatchingServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IPathService, PathService>();

        // Platform-specific patchers
        services.AddSingleton<IPlatformVersionPatcher, AndroidVersionPatcher>();
        services.AddSingleton<IPlatformVersionPatcher, IosVersionPatcher>();
        services.AddSingleton<IPlatformVersionPatcher, WindowsVersionPatcher>();
        services.AddSingleton<IPlatformVersionPatcher, ReleaseVersionPatcher>();

        // Main patching service
        services.AddSingleton<IVersionPatchingService, VersionPatchingService>();

        return services;
    }
}
