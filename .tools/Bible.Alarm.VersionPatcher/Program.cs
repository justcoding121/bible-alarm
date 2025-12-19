using System;
using System.Threading.Tasks;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.VersionPatcher;

internal class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            // Create service collection and register services
            var services = new ServiceCollection();
            services.AddVersionPatchingServices();

            // Build service provider
            await using var serviceProvider = services.BuildServiceProvider();

            // Get the version patching service and execute patching
            var versionPatchingService = serviceProvider.GetRequiredService<IVersionPatchingService>();
            await versionPatchingService.PatchAllPlatformsAsync();

            Console.WriteLine("Version patching completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Version patching failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
