using System.Collections.Concurrent;
using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class IndexServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemServiceMock;
    private readonly IndexService _service;
    private readonly IFixture _fixture;

    public IndexServiceTests()
    {
        _fixture = new Fixture();
        _fileSystemServiceMock = new Mock<IFileSystemService>();
        _service = new IndexService(_fileSystemServiceMock.Object);
    }

    [Fact]
    public async Task WriteBibleIndexAsync_ShouldWriteIndexFiles()
    {
        // Arrange
        var languageCodeToNameMappings = new ConcurrentDictionary<string, string>
        {
            ["E"] = "English",
            ["S"] = "Spanish"
        };

        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>
        {
            ["E"] = new List<string> { "nwt", "bi12" },
            ["S"] = new List<string> { "nwt" }
        };

        _fileSystemServiceMock.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.WriteBibleIndexAsync(languageCodeToNameMappings, languageCodeToEditionsMapping);

        // Assert
        _fileSystemServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteIndexMetadataAsync_ShouldWriteMetadataFile()
    {
        // Arrange
        _fileSystemServiceMock.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.WriteIndexMetadataAsync();

        // Assert
        _fileSystemServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ZipIndexFiles_ShouldCreateZipFile()
    {
        // Arrange
        var indexDirectory = "test_index";
        var zipFilePath = "test_index.zip";

        _fileSystemServiceMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemServiceMock.Setup(x => x.GetFileSize(It.IsAny<string>())).Returns(1024);

        // Act
        _service.ZipIndexFiles();

        // Assert
        _fileSystemServiceMock.Verify(x => x.FileExists(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WriteBibleIndexAsync_WithEmptyMappings_ShouldStillWriteFiles()
    {
        // Arrange
        var emptyLanguageMappings = new ConcurrentDictionary<string, string>();
        var emptyEditionMappings = new ConcurrentDictionary<string, List<string>>();

        _fileSystemServiceMock.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.WriteBibleIndexAsync(emptyLanguageMappings, emptyEditionMappings);

        // Assert
        _fileSystemServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }
}
