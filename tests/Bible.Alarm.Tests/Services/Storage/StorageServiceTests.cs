#nullable enable

using System.Reflection;
using Bible.Alarm.Services.Storage;

namespace Bible.Alarm.Tests;

public sealed class StorageServiceTests
{
    private sealed class TempStorageService : StorageService
    {
        public TempStorageService(string root)
        {
            StorageRoot = root;
            CacheRoot = root;
        }

        public override string StorageRoot { get; }
        public override string CacheRoot { get; }
        public override Assembly MainAssembly => typeof(TempStorageService).Assembly;
    }

    [Fact]
    public async Task SaveFile_and_ReadFile_roundtrip_text()
    {
        var root = Path.Combine(Path.GetTempPath(), "ba-storage-svc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            using var sut = new TempStorageService(root);

            await sut.SaveFile(root, "note.txt", "hello");

            var path = Path.Combine(root, "note.txt");
            Assert.True(await sut.FileExists(path));

            var text = await sut.ReadFile(path);
            Assert.Equal("hello", text);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAllFiles_returns_empty_when_directory_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "ba-storage-none-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missing));

        using var sut = new TempStorageService(Path.GetTempPath());

        var files = await sut.GetAllFiles(missing);

        Assert.Empty(files);
    }
}
