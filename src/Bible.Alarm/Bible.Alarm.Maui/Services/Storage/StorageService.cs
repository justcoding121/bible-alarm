using Bible.Alarm.Services.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    public abstract class StorageService : IStorageService
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
                return new List<string>();
            }

            return Directory.GetFiles(path).ToList();
        }

        public Task<string> ReadFile(string path)
        {
            return Task.FromResult(File.ReadAllText(path));
        }

        public async Task SaveFile(string directoryPath, string name, string contents)
        {
            if (!await DirectoryExists(directoryPath))
            {
                await createDirectory(directoryPath);
            }

            File.WriteAllText(Path.Combine(directoryPath, name), contents);
        }

        public async Task SaveFile(string directoryPath, string name, byte[] contents)
        {
            if (!await DirectoryExists(directoryPath))
            {
                await createDirectory(directoryPath);
            }

            File.WriteAllBytes(Path.Combine(directoryPath, name), contents);
        }

        public async Task CopyResourceFile(string resourceFileName,
            string destinationDirectoryPath, string destinationFileName)
        {
            if (!await DirectoryExists(destinationDirectoryPath))
            {
                await createDirectory(destinationDirectoryPath);
            }

            using (var sr = ResourceLoader.GetEmbeddedResourceStream(MainAssembly, resourceFileName))
            {
                var buffer = new byte[1024];
                using (BinaryWriter fileWriter =
                    new BinaryWriter(File.Create(Path.Combine(destinationDirectoryPath, destinationFileName))))
                {
                    long readCount = 0;
                    while (readCount < sr.Length)
                    {
                        int read = await sr.ReadAsync(buffer, 0, buffer.Length);
                        readCount += read;
                        fileWriter.Write(buffer, 0, read);
                    }

                }
            }

        }

        private Task createDirectory(string path)
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

            return Task.FromResult(new DateTimeOffset(new[] { file.LastAccessTime, file.LastWriteTime, file.CreationTime }.Max()));
        }

        public void Dispose()
        {
        }
    }
}
