using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Integration;

public class HarvesterIntegrationTests : IDisposable
{
    private readonly WireMockServer _wireMockServer;
    private readonly IFixture _fixture;

    public HarvesterIntegrationTests()
    {
        _wireMockServer = WireMockServer.Start();
        _fixture = new Fixture();
    }

    [Fact]
    public async Task BibleHarvesterService_WithMockApi_ShouldProcessResponse()
    {
        // Arrange
        var mockResponse = new
        {
            languages = new[]
            {
                new
                {
                    code = "E",
                    name = "English",
                    editions = new[]
                    {
                        new
                        {
                            code = "nwt",
                            name = "New World Translation"
                    }
                }
            }
        };

        _wireMockServer
            .Given(Request.Create().WithPath("/api/bible").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(mockResponse)));

        var httpClient = new HttpClient { BaseAddress = new Uri(_wireMockServer.Urls[0]) };
        var fileSystemService = new FileSystemService();
        var service = new BibleHarvesterService(fileSystemService, httpClient);

        var bibleMappings = new Dictionary<string, string>
        {
            { "nwt", "New World Translation" }
        };

        var languageMappings = new ConcurrentDictionary<string, string>
        {
            ["E"] = "English"
        };

        var editionMappings = new ConcurrentDictionary<string, List<string>>
        {
            ["E"] = new List<string> { "nwt" }
        };

        // Act
        await service.HarvestBibleLinksAsync(bibleMappings, languageMappings, editionMappings);

        // Assert
        _wireMockServer.LogEntries.Should().HaveCount(1);
        var logEntry = _wireMockServer.LogEntries.First();
        logEntry.RequestMessage.Path.Should().Be("/api/bible");
    }

    [Fact]
    public async Task MusicHarvesterService_WithMockApi_ShouldProcessResponse()
    {
        // Arrange
        var mockResponse = new
        {
            music = new[]
            {
                new
                {
                    type = "vocal",
                    title = "Test Song",
                    url = "https://example.com/song.mp3"
                }
            }
        };

        _wireMockServer
            .Given(Request.Create().WithPath("/api/music").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(mockResponse)));

        var httpClient = new HttpClient { BaseAddress = new Uri(_wireMockServer.Urls[0]) };
        var fileSystemService = new FileSystemService();
        var service = new MusicHarvesterService(fileSystemService, httpClient);

        // Act
        await service.HarvestVocalMusicLinksAsync();

        // Assert
        _wireMockServer.LogEntries.Should().HaveCount(1);
        var logEntry = _wireMockServer.LogEntries.First();
        logEntry.RequestMessage.Path.Should().Be("/api/music");
    }

    [Fact]
    public async Task HarvestingOrchestratorService_WithMockApi_ShouldExecuteFullWorkflow()
    {
        // Arrange
        var mockBibleResponse = new
        {
            languages = new[]
            {
                new
                {
                    code = "E",
                    name = "English",
                    editions = new[]
                    {
                        new
                        {
                            code = "nwt",
                            name = "New World Translation"
                        }
                    }
                }
            }
        };

        var mockMusicResponse = new
        {
            music = new[]
            {
                new
                {
                    type = "vocal",
                    title = "Test Song",
                    url = "https://example.com/song.mp3"
                }
            }
        };

        _wireMockServer
            .Given(Request.Create().WithPath("/api/bible").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(mockBibleResponse)));

        _wireMockServer
            .Given(Request.Create().WithPath("/api/music").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(mockMusicResponse)));

        var httpClient = new HttpClient { BaseAddress = new Uri(_wireMockServer.Urls[0]) };
        var fileSystemService = new FileSystemService();
        var configurationService = new ConfigurationService();
        var s3Client = new MockS3Client();
        var cloudPublishingService = new CloudPublishingService(s3Client, configurationService);

        var bibleHarvesterService = new BibleHarvesterService(fileSystemService, httpClient);
        var musicHarvesterService = new MusicHarvesterService(fileSystemService, httpClient);
        var indexService = new IndexService(fileSystemService);

        var orchestrator = new HarvestingOrchestratorService(
            bibleHarvesterService,
            musicHarvesterService,
            indexService,
            cloudPublishingService);

        // Act
        await orchestrator.ExecuteHarvestingAsync();

        // Assert
        _wireMockServer.LogEntries.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _wireMockServer?.Dispose();
    }
}

// Mock S3 client for integration tests
public class MockS3Client : IS3Client
{
    public Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request)
    {
        return Task.FromResult(new ListObjectsResponse { S3Objects = new List<S3Object>() });
    }

    public Task PutObjectAsync(PutObjectRequest request)
    {
        return Task.CompletedTask;
    }

    public Task DeleteObjectsAsync(DeleteObjectsRequest request)
    {
        return Task.CompletedTask;
    }
}
