using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IFileSystemService
{
    Task CleanupIndexDirectoryAsync();
    Task EnsureDirectoryExistsAsync(string path);
    Task<bool> ValidateIndexFileAsync();
}
