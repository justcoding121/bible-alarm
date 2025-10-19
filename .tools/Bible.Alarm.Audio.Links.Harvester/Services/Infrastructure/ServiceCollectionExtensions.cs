using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHarvesterServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        
        // Harvester services
        services.AddSingleton<IBibleHarvesterService, BibleHarvesterService>();
        services.AddSingleton<IMusicHarvesterService, MusicHarvesterService>();
        
        // Index and publishing services
        services.AddSingleton<IIndexService, IndexService>();
        services.AddSingleton<ICloudPublishingService, CloudPublishingService>();
        
        // Orchestrator service
        services.AddSingleton<IHarvestingOrchestratorService, HarvestingOrchestratorService>();

        return services;
    }
}