using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IFileSystemService
{
    Task CleanupIndexDirectoryAsync();
    Task EnsureDirectoryExistsAsync(string path);
    Task<bool> ValidateIndexFileAsync();
    Task WriteFileAsync(string path, string content);
    Task<string> ReadFileAsync(string path);
    bool FileExists(string path);
    long GetFileSize(string path);
    void CreateDirectory(string path);
    void DeleteDirectory(string path);
    void ZipFile(string sourcePath, string destinationPath);
}
