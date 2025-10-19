using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class MusicHarvesterServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemServiceMock;
    private readonly Mock<IHttpClient> _httpClientMock;
    private readonly MusicHarvesterService _service;
    private readonly IFixture _fixture;

    public MusicHarvesterServiceTests()
    {
        _fixture = new Fixture();
        _fileSystemServiceMock = new Mock<IFileSystemService>();
        _httpClientMock = new Mock<IHttpClient>();
        _service = new MusicHarvesterService(_fileSystemServiceMock.Object, _httpClientMock.Object);
    }

    [Fact]
    public async Task HarvestVocalMusicLinksAsync_ShouldProcessVocalMusic()
    {
        // Arrange
        var mockResponse = @"{
            ""music"": [
                {
                    ""type"": ""vocal"",
                    ""title"": ""Test Song"",
                    ""url"": ""https://example.com/song.mp3""
                }
            ]
        }";

        _httpClientMock.Setup(x => x.GetStringAsync(It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        _fileSystemServiceMock.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HarvestVocalMusicLinksAsync();

        // Assert
        _httpClientMock.Verify(x => x.GetStringAsync(It.IsAny<string>()), Times.AtLeastOnce);
        _fileSystemServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HarvestMelodyMusicLinksAsync_ShouldProcessMelodyMusic()
    {
        // Arrange
        var mockResponse = @"{
            ""music"": [
                {
                    ""type"": ""melody"",
                    ""title"": ""Test Melody"",
                    ""url"": ""https://example.com/melody.mp3""
                }
            ]
        }";

        _httpClientMock.Setup(x => x.GetStringAsync(It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        _fileSystemServiceMock.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HarvestMelodyMusicLinksAsync();

        // Assert
        _httpClientMock.Verify(x => x.GetStringAsync(It.IsAny<string>()), Times.AtLeastOnce);
        _fileSystemServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HarvestVocalMusicLinksAsync_WhenHttpClientFails_ShouldThrowException()
    {
        // Arrange
        _httpClientMock.Setup(x => x.GetStringAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var action = async () => await _service.HarvestVocalMusicLinksAsync();
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task HarvestMelodyMusicLinksAsync_WhenHttpClientFails_ShouldThrowException()
    {
        // Arrange
        _httpClientMock.Setup(x => x.GetStringAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var action = async () => await _service.HarvestMelodyMusicLinksAsync();
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
