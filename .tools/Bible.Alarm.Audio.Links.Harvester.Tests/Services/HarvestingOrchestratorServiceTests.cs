using System.Collections.Concurrent;
using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class HarvestingOrchestratorServiceTests
{
    private readonly Mock<IBibleHarvesterService> _bibleHarvesterMock;
    private readonly Mock<IMusicHarvesterService> _musicHarvesterMock;
    private readonly Mock<IIndexService> _indexServiceMock;
    private readonly Mock<ICloudPublishingService> _cloudPublishingMock;
    private readonly Mock<IFileSystemService> _fileSystemServiceMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly HarvestingOrchestratorService _service;
    private readonly IFixture _fixture;

    public HarvestingOrchestratorServiceTests()
    {
        _fixture = new Fixture();
        _bibleHarvesterMock = new Mock<IBibleHarvesterService>();
        _musicHarvesterMock = new Mock<IMusicHarvesterService>();
        _indexServiceMock = new Mock<IIndexService>();
        _cloudPublishingMock = new Mock<ICloudPublishingService>();
        _fileSystemServiceMock = new Mock<IFileSystemService>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _service = new HarvestingOrchestratorService(
            _bibleHarvesterMock.Object,
            _musicHarvesterMock.Object,
            _indexServiceMock.Object,
            _cloudPublishingMock.Object,
            _fileSystemServiceMock.Object,
            _configurationServiceMock.Object);
    }

    [Fact]
    public async Task ExecuteHarvestingAsync_ShouldExecuteAllSteps()
    {
        // Arrange
        var bibleMappings = new Dictionary<string, string>();
        var languageMappings = new ConcurrentDictionary<string, string>();
        var editionMappings = new ConcurrentDictionary<string, List<string>>();

        _bibleHarvesterMock.Setup(x => x.HarvestBibleLinksAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestVocalMusicLinksAsync())
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestMelodyMusicLinksAsync())
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.WriteBibleIndexAsync(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.WriteIndexMetadataAsync())
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.ZipIndexFiles());

        _cloudPublishingMock.Setup(x => x.PublishIndexToCloudFrontAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteHarvestingAsync();

        // Assert
        _bibleHarvesterMock.Verify(x => x.HarvestBibleLinksAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()), Times.Once);
        _musicHarvesterMock.Verify(x => x.HarvestVocalMusicLinksAsync(), Times.Once);
        _musicHarvesterMock.Verify(x => x.HarvestMelodyMusicLinksAsync(), Times.Once);
        _indexServiceMock.Verify(x => x.WriteBibleIndexAsync(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()), Times.Once);
        _indexServiceMock.Verify(x => x.WriteIndexMetadataAsync(), Times.Once);
        _indexServiceMock.Verify(x => x.ZipIndexFiles(), Times.Once);
        _cloudPublishingMock.Verify(x => x.PublishIndexToCloudFrontAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteHarvestingAsync_WhenBibleHarvestingFails_ShouldThrowException()
    {
        // Arrange
        _bibleHarvesterMock.Setup(x => x.HarvestBibleLinksAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .ThrowsAsync(new Exception("Bible harvesting failed"));

        // Act & Assert
        var action = async () => await _service.ExecuteHarvestingAsync();
        await action.Should().ThrowAsync<Exception>().WithMessage("Bible harvesting failed");
    }

    [Fact]
    public async Task ExecuteHarvestingAsync_WhenMusicHarvestingFails_ShouldThrowException()
    {
        // Arrange
        _bibleHarvesterMock.Setup(x => x.HarvestBibleLinksAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestVocalMusicLinksAsync())
            .ThrowsAsync(new Exception("Music harvesting failed"));

        // Act & Assert
        var action = async () => await _service.ExecuteHarvestingAsync();
        await action.Should().ThrowAsync<Exception>().WithMessage("Music harvesting failed");
    }

    [Fact]
    public async Task ExecuteHarvestingAsync_WhenIndexServiceFails_ShouldThrowException()
    {
        // Arrange
        _bibleHarvesterMock.Setup(x => x.HarvestBibleLinksAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestVocalMusicLinksAsync())
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestMelodyMusicLinksAsync())
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.WriteBibleIndexAsync(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .ThrowsAsync(new Exception("Index service failed"));

        // Act & Assert
        var action = async () => await _service.ExecuteHarvestingAsync();
        await action.Should().ThrowAsync<Exception>().WithMessage("Index service failed");
    }

    [Fact]
    public async Task ExecuteHarvestingAsync_WhenCloudPublishingFails_ShouldThrowException()
    {
        // Arrange
        _bibleHarvesterMock.Setup(x => x.HarvestBibleLinksAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestVocalMusicLinksAsync())
            .Returns(Task.CompletedTask);

        _musicHarvesterMock.Setup(x => x.HarvestMelodyMusicLinksAsync())
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.WriteBibleIndexAsync(It.IsAny<ConcurrentDictionary<string, string>>(), It.IsAny<ConcurrentDictionary<string, List<string>>>()))
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.WriteIndexMetadataAsync())
            .Returns(Task.CompletedTask);

        _indexServiceMock.Setup(x => x.ZipIndexFiles());

        _cloudPublishingMock.Setup(x => x.PublishIndexToCloudFrontAsync())
            .ThrowsAsync(new Exception("Cloud publishing failed"));

        // Act & Assert
        var action = async () => await _service.ExecuteHarvestingAsync();
        await action.Should().ThrowAsync<Exception>().WithMessage("Cloud publishing failed");
    }
}
