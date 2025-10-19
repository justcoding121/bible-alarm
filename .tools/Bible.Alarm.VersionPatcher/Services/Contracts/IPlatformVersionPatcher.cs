using System.Threading.Tasks;

namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IPlatformVersionPatcher
{
    Task PatchVersionAsync();
    string PlatformName { get; }
}
