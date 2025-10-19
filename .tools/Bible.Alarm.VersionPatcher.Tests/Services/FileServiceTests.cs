using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using System.IO;
using Xunit;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly FileService _fileService;
    private readonly IFixture _fixture;
    private readonly string _tempDirectory;

    public FileServiceTests()
    {
        _fileService = new FileService();
        _fixture = new Fixture();
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ReadFileAsync_WithExistingFile_ShouldReturnContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test.txt");
        var expectedContent = "Hello, World!";
        await File.WriteAllTextAsync(filePath, expectedContent);

        // Act
        var result = await _fileService.ReadFileAsync(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task WriteFileAsync_ShouldCreateFileWithContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "write_test.txt");
        var content = "Test content";

        // Act
        await _fileService.WriteFileAsync(filePath, content);

        // Assert
        var result = await File.ReadAllTextAsync(filePath);
        result.Should().Be(content);
    }

    [Fact]
    public void FileExists_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "exists_test.txt");
        File.WriteAllText(filePath, "test");

        // Act
        var result = _fileService.FileExists(filePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "non_existent.txt");

        // Act
        var result = _fileService.FileExists(filePath);

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
