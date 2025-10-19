using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using System.IO;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class FileSystemServiceTests : IDisposable
{
    private readonly FileSystemService _fileSystemService;
    private readonly IFixture _fixture;
    private readonly string _tempDirectory;

    public FileSystemServiceTests()
    {
        _fileSystemService = new FileSystemService();
        _fixture = new Fixture();
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task WriteAllTextAsync_ShouldCreateFileWithContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "test.txt");
        var content = "Test content";

        // Act
        await _fileSystemService.WriteAllTextAsync(filePath, content);

        // Assert
        var result = await File.ReadAllTextAsync(filePath);
        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadAllTextAsync_WithExistingFile_ShouldReturnContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "read_test.txt");
        var expectedContent = "Hello, World!";
        await File.WriteAllTextAsync(filePath, expectedContent);

        // Act
        var result = await _fileSystemService.ReadAllTextAsync(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public void FileExists_WithExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "exists_test.txt");
        File.WriteAllText(filePath, "test");

        // Act
        var result = _fileSystemService.FileExists(filePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "non_existent.txt");

        // Act
        var result = _fileSystemService.FileExists(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetFileSize_WithExistingFile_ShouldReturnCorrectSize()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "size_test.txt");
        var content = "Test content";
        File.WriteAllText(filePath, content);
        var expectedSize = new FileInfo(filePath).Length;

        // Act
        var result = _fileSystemService.GetFileSize(filePath);

        // Assert
        result.Should().Be(expectedSize);
    }

    [Fact]
    public void EnsureDirectoryExists_ShouldCreateDirectory()
    {
        // Arrange
        var directoryPath = Path.Combine(_tempDirectory, "new_directory");

        // Act
        _fileSystemService.EnsureDirectoryExists(directoryPath);

        // Assert
        Directory.Exists(directoryPath).Should().BeTrue();
    }

    [Fact]
    public void DeleteDirectory_ShouldRemoveDirectory()
    {
        // Arrange
        var directoryPath = Path.Combine(_tempDirectory, "to_delete");
        Directory.CreateDirectory(directoryPath);

        // Act
        _fileSystemService.DeleteDirectory(directoryPath);

        // Assert
        Directory.Exists(directoryPath).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
