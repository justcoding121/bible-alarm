using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class CloudPublishingServiceTests
{
    private readonly Mock<IS3Client> _s3ClientMock;
    private readonly Mock<IConfigurationService> _configurationServiceMock;
    private readonly CloudPublishingService _service;
    private readonly IFixture _fixture;

    public CloudPublishingServiceTests()
    {
        _fixture = new Fixture();
        _s3ClientMock = new Mock<IS3Client>();
        _configurationServiceMock = new Mock<IConfigurationService>();
        _service = new CloudPublishingService(_s3ClientMock.Object, _configurationServiceMock.Object);
    }

    [Fact]
    public async Task PublishIndexToCloudFrontAsync_ShouldUploadFilesToS3()
    {
        // Arrange
        var bucketName = "test-bucket";
        var keyPrefix = "media-index/";
        var region = "us-east-1";

        _configurationServiceMock.Setup(x => x.S3BucketName).Returns(bucketName);
        _configurationServiceMock.Setup(x => x.S3KeyPrefix).Returns(keyPrefix);
        _configurationServiceMock.Setup(x => x.AwsRegion).Returns(region);

        _s3ClientMock.Setup(x => x.ListObjectsAsync(It.IsAny<ListObjectsRequest>()))
            .ReturnsAsync(new ListObjectsResponse { S3Objects = new List<S3Object>() });

        _s3ClientMock.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.PublishIndexToCloudFrontAsync();

        // Assert
        _s3ClientMock.Verify(x => x.ListObjectsAsync(It.IsAny<ListObjectsRequest>()), Times.Once);
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishIndexToCloudFrontAsync_WhenS3ClientFails_ShouldThrowException()
    {
        // Arrange
        var bucketName = "test-bucket";
        var keyPrefix = "media-index/";
        var region = "us-east-1";

        _configurationServiceMock.Setup(x => x.S3BucketName).Returns(bucketName);
        _configurationServiceMock.Setup(x => x.S3KeyPrefix).Returns(keyPrefix);
        _configurationServiceMock.Setup(x => x.AwsRegion).Returns(region);

        _s3ClientMock.Setup(x => x.ListObjectsAsync(It.IsAny<ListObjectsRequest>()))
            .ThrowsAsync(new Exception("S3 error"));

        // Act & Assert
        var action = async () => await _service.PublishIndexToCloudFrontAsync();
        await action.Should().ThrowAsync<Exception>().WithMessage("S3 error");
    }

    [Fact]
    public async Task PublishIndexToCloudFrontAsync_ShouldDeleteExistingObjects()
    {
        // Arrange
        var bucketName = "test-bucket";
        var keyPrefix = "media-index/";
        var region = "us-east-1";

        _configurationServiceMock.Setup(x => x.S3BucketName).Returns(bucketName);
        _configurationServiceMock.Setup(x => x.S3KeyPrefix).Returns(keyPrefix);
        _configurationServiceMock.Setup(x => x.AwsRegion).Returns(region);

        var existingObjects = new List<S3Object>
        {
            new S3Object { Key = "media-index/old-file1.json" },
            new S3Object { Key = "media-index/old-file2.json" }
        };

        _s3ClientMock.Setup(x => x.ListObjectsAsync(It.IsAny<ListObjectsRequest>()))
            .ReturnsAsync(new ListObjectsResponse { S3Objects = existingObjects });

        _s3ClientMock.Setup(x => x.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>()))
            .Returns(Task.CompletedTask);

        _s3ClientMock.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.PublishIndexToCloudFrontAsync();

        // Assert
        _s3ClientMock.Verify(x => x.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>()), Times.Once);
    }
}
