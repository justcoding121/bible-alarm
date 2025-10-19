using System.Threading.Tasks;

namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IFileService
{
    Task<string> ReadFileAsync(string filePath);
    Task WriteFileAsync(string filePath, string content);
    bool FileExists(string filePath);
}
