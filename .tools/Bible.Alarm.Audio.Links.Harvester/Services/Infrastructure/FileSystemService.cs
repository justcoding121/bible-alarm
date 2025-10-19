using System;
using System.IO;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Utilities;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class FileSystemService : IFileSystemService
{
    public async Task CleanupIndexDirectoryAsync()
    {
        await Task.Run(() => DeleteDirectory(DirectoryHelper.IndexDirectory));
    }

    public async Task EnsureDirectoryExistsAsync(string path)
    {
        await Task.Run(() => DirectoryHelper.Ensure(path));
    }

    public async Task<bool> ValidateIndexFileAsync()
    {
        var zipIndex = $"{DirectoryHelper.IndexDirectory}/index.zip";
        return await Task.FromResult(File.Exists(zipIndex));
    }

    /// <summary>
    /// Depth-first recursive delete, with handling for descendant 
    /// directories open in Windows Explorer.
    /// </summary>
    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        foreach (var directory in Directory.GetDirectories(path)) 
            DeleteDirectory(directory);

        var files = Directory.GetFiles(path);

        foreach (var file in files)
            if (!file.EndsWith("index.zip"))
                File.Delete(file);

        if (Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > 0)
            return;

        try
        {
            Directory.Delete(path);
        }
        catch (IOException)
        {
            Directory.Delete(path);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(path);
        }
    }
}
