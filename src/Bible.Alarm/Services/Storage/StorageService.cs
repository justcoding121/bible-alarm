using System.Reflection;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Storage;

public abstract class StorageService : IStorageService, IDisposable
{
    public abstract string StorageRoot { get; }
    public abstract string CacheRoot { get; }
    public abstract Assembly MainAssembly { get; }

    public Task DeleteFile(string path)
    {
        File.Delete(path);
        return Task.FromResult(false);
    }

    public Task<bool> FileExists(string path)
    {
        return Task.FromResult(File.Exists(path));
    }

    public Task<bool> DirectoryExists(string path)
    {
        return Task.FromResult(Directory.Exists(path));
    }

    public Task<DirectoryInfo> CreateDirectory(string path)
    {
        return Task.FromResult(Directory.CreateDirectory(path));
    }

    public Task DeleteDirectory(string path)
    {
        Directory.Delete(path, true);
        return Task.CompletedTask;
    }

    public async Task<List<string>> GetAllFiles(string path)
    {
        if (!await DirectoryExists(path))
        {
            return [];
        }

        return [.. Directory.GetFiles(path)];
    }

    public Task<string> ReadFile(string path)
    {
        return Task.FromResult(File.ReadAllText(path));
    }

    public async Task SaveFile(string directoryPath, string name, string contents)
    {
        if (!await DirectoryExists(directoryPath))
        {
            await CreateDirectoryInternal(directoryPath);
        }

        File.WriteAllText(Path.Combine(directoryPath, name), contents);
    }

    public async Task SaveFile(string directoryPath, string name, byte[] contents)
    {
        if (!await DirectoryExists(directoryPath))
        {
            await CreateDirectoryInternal(directoryPath);
        }

        File.WriteAllBytes(Path.Combine(directoryPath, name), contents);
    }

    public async Task CopyResourceFile(string resourceFileName,
        string destinationDirectoryPath, string destinationFileName)
    {
        if (!await DirectoryExists(destinationDirectoryPath))
        {
            await CreateDirectoryInternal(destinationDirectoryPath);
        }

        var destinationFilePath = Path.Combine(destinationDirectoryPath, destinationFileName);

        // Delete the file if it already exists to avoid IOException
        if (await FileExists(destinationFilePath))
        {
            await DeleteFile(destinationFilePath);
        }

        try
        {
            await using var sr = ResourceLoader.GetEmbeddedResourceStream(MainAssembly, resourceFileName);
            var buffer = new byte[1024];
            await using var fileWriter =
                new BinaryWriter(File.Create(destinationFilePath));
            long readCount = 0;
            while (readCount < sr.Length)
            {
                var read = await sr.ReadAsync(buffer, 0, buffer.Length);
                readCount += read;
                fileWriter.Write(buffer, 0, read);
            }
        }
        catch (Exception ex)
        {
            var availableResources = string.Join(", ", MainAssembly.GetManifestResourceNames());
            var errorMessage = $"Failed to copy embedded resource '{resourceFileName}'. " +
                              $"Available resources: {availableResources}. " +
                              $"Make sure the file is included as an EmbeddedResource in the project file.";

            Log.Logger.Error(ex, errorMessage);

            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    private static Task CreateDirectoryInternal(string path)
    {
        Directory.CreateDirectory(path);
        return Task.FromResult(false);
    }

    public Task<DateTimeOffset> GetFileCreationDate(string pathOrName, bool isResourceFile)
    {
        FileInfo file;
        if (isResourceFile)
        {
            file = ResourceLoader.GetFileInfo(MainAssembly);
        }
        else
        {
            file = new FileInfo(pathOrName);
        }

        return Task.FromResult(
            new DateTimeOffset(new[] { file.LastAccessTime, file.LastWriteTime, file.CreationTime }.Max()));
    }

    public void Dispose()
    {
    }
}