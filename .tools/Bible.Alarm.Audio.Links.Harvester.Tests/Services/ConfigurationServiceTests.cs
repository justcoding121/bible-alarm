using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class ConfigurationServiceTests
{
    private readonly ConfigurationService _configurationService;
    private readonly IFixture _fixture;

    public ConfigurationServiceTests()
    {
        _configurationService = new ConfigurationService();
        _fixture = new Fixture();
    }

    [Fact]
    public void IndexDirectory_ShouldReturnValidPath()
    {
        // Act
        var result = _configurationService.IndexDirectory;

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("index");
    }

    [Fact]
    public void S3BucketName_ShouldReturnValidBucketName()
    {
        // Act
        var result = _configurationService.S3BucketName;

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("bible-alarm");
    }

    [Fact]
    public void S3KeyPrefix_ShouldReturnValidPrefix()
    {
        // Act
        var result = _configurationService.S3KeyPrefix;

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("media-index");
    }

    [Fact]
    public void AwsRegion_ShouldReturnValidRegion()
    {
        // Act
        var result = _configurationService.AwsRegion;

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("us-east-1");
    }
}
