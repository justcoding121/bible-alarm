#nullable enable
using System.Threading.Tasks;

namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IVersionPatchingService
{
    /// <summary>Patch version for all platforms, or a single platform when platformName is set (e.g. "Windows", "Android", "iOS").</summary>
    Task PatchAsync(string? platformName = null);
}
