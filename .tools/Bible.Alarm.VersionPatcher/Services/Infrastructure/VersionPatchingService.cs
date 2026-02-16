#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class VersionPatchingService(IEnumerable<IPlatformVersionPatcher> platformPatchers) : IVersionPatchingService
{
    private readonly IEnumerable<IPlatformVersionPatcher> platformPatchers = platformPatchers ?? throw new ArgumentNullException(nameof(platformPatchers));

    public async Task PatchAsync(string? platformName = null)
    {
        var toRun = string.IsNullOrWhiteSpace(platformName)
            ? platformPatchers.ToList()
            : platformPatchers.Where(p => string.Equals(p.PlatformName, platformName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (toRun.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(platformName))
                throw new ArgumentException($"Unknown platform: '{platformName}'. Use Windows, Android, iOS, or Release.", nameof(platformName));
            return;
        }

        Console.WriteLine(toRun.Count == platformPatchers.Count()
            ? "Starting version patching for all platforms..."
            : $"Starting version patching for {platformName} only...");

        var tasks = toRun.Select(async patcher =>
        {
            try
            {
                Console.WriteLine($"Patching {patcher.PlatformName}...");
                await patcher.PatchVersionAsync();
                Console.WriteLine($"{patcher.PlatformName} patching completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error patching {patcher.PlatformName}: {ex.Message}");
                throw;
            }
        });

        await Task.WhenAll(tasks);
        Console.WriteLine("Version patching completed successfully!");
    }
}
