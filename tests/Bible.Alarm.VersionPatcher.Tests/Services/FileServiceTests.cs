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
        // Arrange
        var filePath = Path.Combine(tempDirectory, "test.txt");
        var expectedContent = "Hello, World!";
        await File.WriteAllTextAsync(filePath, expectedContent);

        // Act
        var result = await fileService.ReadFileAsync(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task WriteFileAsync_ShouldCreateFileWithContent()
    {
        // Arrange
        var filePath = Path.Combine(tempDirectory, "write_test.txt");
        var content = "Test content";

        // Act
        await fileService.WriteFileAsync(filePath, content);

        // Assert
        var result = await File.ReadAllTextAsync(filePath);
        result.Should().Be(content);
    }

    [Fact]
    public void FileExists_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var filePath = Path.Combine(tempDirectory, "exists_test.txt");
        File.WriteAllText(filePath, "test");

        // Act
        var result = fileService.FileExists(filePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var filePath = Path.Combine(tempDirectory, "non_existent.txt");

        // Act
        var result = fileService.FileExists(filePath);

        // Assert
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
