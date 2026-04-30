#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.VersionPatcher;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            var platform = GetPlatformFilter(args);
            if (!string.IsNullOrEmpty(platform))
                Console.WriteLine($"Platform filter: {platform}");

            var services = new ServiceCollection();
            services.AddVersionPatchingServices();

            await using var serviceProvider = services.BuildServiceProvider();

            var versionPatchingService = serviceProvider.GetRequiredService<IVersionPatchingService>();
            await versionPatchingService.PatchAsync(platform);

            Console.WriteLine("Version patching completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Version patching failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    private static string? GetPlatformFilter(string[] args)
    {
        var platformArg = args.FirstOrDefault(a => a.StartsWith("--platform=", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(platformArg))
            return platformArg["--platform=".Length..].Trim();

        return Environment.GetEnvironmentVariable("VERSION_PATCH_PLATFORM")?.Trim();
    }
}
