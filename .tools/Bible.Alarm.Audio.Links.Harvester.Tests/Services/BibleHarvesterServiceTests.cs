using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class BibleHarvesterServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemServiceMock;
    private readonly Mock<IHttpClient> _httpClientMock;
    private readonly BibleHarvesterService _service;
    private readonly IFixture _fixture;

    public BibleHarvesterServiceTests()
    {
        _fixture = new Fixture();
        _fileSystemServiceMock = new Mock<IFileSystemService>();
        _httpClientMock = new Mock<IHttpClient>();
        _service = new BibleHarvesterService(_fileSystemServiceMock.Object, _httpClientMock.Object);
    }

    [Fact]
    public async Task HarvestBibleLinksAsync_ShouldProcessAllPublications()
    {
        // Arrange
        var biblePublicationCodeToNameMappings = new Dictionary<string, string>
        {
            { "nwt", "New World Translation" },
            { "bi12", "Bible Stories" }
        };

        var languageCodeToNameMappings = new ConcurrentDictionary<string, string>
        {
            ["E"] = "English"
        };

        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>
        {
            ["E"] = new List<string> { "nwt", "bi12" }
        };

        var mockResponse = @"{
            ""languages"": [
                {
                    ""code"": ""E"",
                    ""name"": ""English"",
                    ""editions"": [
                        {
                            ""code"": ""nwt"",
                            ""name"": ""New World Translation""
                        }
                    ]
                }
            ]
        }";

        _httpClientMock.Setup(x => x.GetStringAsync(It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        _fileSystemServiceMock.Setup(x => x.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HarvestBibleLinksAsync(
            biblePublicationCodeToNameMappings,
            languageCodeToNameMappings,
            languageCodeToEditionsMapping);

        // Assert
        _httpClientMock.Verify(x => x.GetStringAsync(It.IsAny<string>()), Times.AtLeastOnce);
        _fileSystemServiceMock.Verify(x => x.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HarvestBibleLinksAsync_WithEmptyMappings_ShouldNotProcess()
    {
        // Arrange
        var emptyBibleMappings = new Dictionary<string, string>();
        var emptyLanguageMappings = new ConcurrentDictionary<string, string>();
        var emptyEditionMappings = new ConcurrentDictionary<string, List<string>>();

        // Act
        await _service.HarvestBibleLinksAsync(
            emptyBibleMappings,
            emptyLanguageMappings,
            emptyEditionMappings);

        // Assert
        _httpClientMock.Verify(x => x.GetStringAsync(It.IsAny<string>()), Times.Never);
        _fileSystemServiceMock.Verify(x => x.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HarvestBibleLinksAsync_WhenHttpClientFails_ShouldThrowException()
    {
        // Arrange
        var biblePublicationCodeToNameMappings = new Dictionary<string, string>
        {
            { "nwt", "New World Translation" }
        };

        var languageCodeToNameMappings = new ConcurrentDictionary<string, string>
        {
            ["E"] = "English"
        };

        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>
        {
            ["E"] = new List<string> { "nwt" }
        };

        _httpClientMock.Setup(x => x.GetStringAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var action = async () => await _service.HarvestBibleLinksAsync(
            biblePublicationCodeToNameMappings,
            languageCodeToNameMappings,
            languageCodeToEditionsMapping);

        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
