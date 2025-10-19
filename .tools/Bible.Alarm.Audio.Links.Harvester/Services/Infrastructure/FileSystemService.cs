using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Utilities;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class FileSystemService : IFileSystemService
{
    public async Task CleanupIndexDirectoryAsync()
    {
        await Task.Run(() => DeleteDirectoryRecursive(DirectoryHelper.IndexDirectory));
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

    public async Task WriteFileAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content);
    }

    public async Task<string> ReadFileAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public long GetFileSize(string path)
    {
        var fileInfo = new FileInfo(path);
        return fileInfo.Exists ? fileInfo.Length : 0;
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    public void ZipFile(string sourcePath, string destinationPath)
    {
        System.IO.Compression.ZipFile.CreateFromDirectory(sourcePath, destinationPath);
    }

    /// <summary>
    /// Depth-first recursive delete, with handling for descendant 
    /// directories open in Windows Explorer.
    /// </summary>
    private static void DeleteDirectoryRecursive(string path)
    {
        if (!Directory.Exists(path)) return;

        foreach (var directory in Directory.GetDirectories(path)) 
            DeleteDirectoryRecursive(directory);

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
