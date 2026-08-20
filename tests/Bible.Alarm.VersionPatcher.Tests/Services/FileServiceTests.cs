using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly FileService fileService;
    private readonly IFixture fixture;
    private readonly string tempDirectory;

    public FileServiceTests()
    {
        fileService = new FileService();
        fixture = new Fixture();
        tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public async Task ReadFileAsync_WithExistingFile_ShouldReturnContent()
    {
        var filePath = Path.Combine(tempDirectory, "test.txt");
        var expectedContent = "Hello, World!";
        await File.WriteAllTextAsync(filePath, expectedContent);

        var result = await fileService.ReadFileAsync(filePath);

        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task WriteFileAsync_ShouldCreateFileWithContent()
    {
        var filePath = Path.Combine(tempDirectory, "write_test.txt");
        var content = "Test content";

        await fileService.WriteFileAsync(filePath, content);

        var result = await File.ReadAllTextAsync(filePath);
        result.Should().Be(content);
    }

    [Fact]
    public void FileExists_WithExistingFile_ShouldReturnTrue()
    {
        var filePath = Path.Combine(tempDirectory, "exists_test.txt");
        File.WriteAllText(filePath, "test");

        var result = fileService.FileExists(filePath);

        result.Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ShouldReturnFalse()
    {
        var filePath = Path.Combine(tempDirectory, "non_existent.txt");

        var result = fileService.FileExists(filePath);

        result.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
