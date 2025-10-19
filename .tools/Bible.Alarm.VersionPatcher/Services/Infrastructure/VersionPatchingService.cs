using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.VersionPatcher.Services.Contracts;

namespace Bible.Alarm.VersionPatcher.Services.Infrastructure;

public class VersionPatchingService : IVersionPatchingService
{
    private readonly IEnumerable<IPlatformVersionPatcher> _platformPatchers;

    public VersionPatchingService(IEnumerable<IPlatformVersionPatcher> platformPatchers)
    {
        _platformPatchers = platformPatchers ?? throw new ArgumentNullException(nameof(platformPatchers));
    }

    public async Task PatchAllPlatformsAsync()
    {
        Console.WriteLine("Starting version patching for all platforms...");

        var tasks = _platformPatchers.Select(async patcher =>
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
        Console.WriteLine("All platform version patching completed successfully!");
    }
}
