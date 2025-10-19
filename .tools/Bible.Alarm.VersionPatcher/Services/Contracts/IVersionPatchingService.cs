using System.Threading.Tasks;

namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IVersionPatchingService
{
    Task PatchAllPlatformsAsync();
}
