using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class VersionPatchingServiceTests
{
    private readonly Mock<IPlatformVersionPatcher> androidPatcherMock;
    private readonly Mock<IPlatformVersionPatcher> iosPatcherMock;
    private readonly Mock<IPlatformVersionPatcher> windowsPatcherMock;
    private readonly VersionPatchingService service;
    private readonly IFixture fixture;

    public VersionPatchingServiceTests()
    {
        fixture = new Fixture();
        androidPatcherMock = new Mock<IPlatformVersionPatcher>();
        iosPatcherMock = new Mock<IPlatformVersionPatcher>();
        windowsPatcherMock = new Mock<IPlatformVersionPatcher>();

        var patchers = new List<IPlatformVersionPatcher>
        {
            androidPatcherMock.Object,
            iosPatcherMock.Object,
            windowsPatcherMock.Object
        };

        service = new VersionPatchingService(patchers);
    }

    [Fact]
    public async Task PatchAsync_WhenNoPlatformFilter_ShouldCallAllPatchers()
    {
        // Arrange
        androidPatcherMock.Setup(x => x.PlatformName).Returns("Android");
        iosPatcherMock.Setup(x => x.PlatformName).Returns("iOS");
        windowsPatcherMock.Setup(x => x.PlatformName).Returns("Windows");

        androidPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);
        iosPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);
        windowsPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);

        // Act
        await service.PatchAsync(null);

        // Assert
        androidPatcherMock.Verify(x => x.PatchVersionAsync(), Times.Once);
        iosPatcherMock.Verify(x => x.PatchVersionAsync(), Times.Once);
        windowsPatcherMock.Verify(x => x.PatchVersionAsync(), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_WhenOnePatcherFails_ShouldThrowException()
    {
        // Arrange
        androidPatcherMock.Setup(x => x.PlatformName).Returns("Android");
        iosPatcherMock.Setup(x => x.PlatformName).Returns("iOS");
        windowsPatcherMock.Setup(x => x.PlatformName).Returns("Windows");

        androidPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);
        iosPatcherMock.Setup(x => x.PatchVersionAsync()).ThrowsAsync(new Exception("iOS patcher failed"));
        windowsPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);

        // Act & Assert
        var action = () => service.PatchAsync(null);
        await action.Should().ThrowAsync<Exception>().WithMessage("iOS patcher failed");
    }

    [Fact]
    public async Task PatchAsync_WithEmptyPatchersList_ShouldCompleteSuccessfully()
    {
        // Arrange
        var emptyPatchers = new List<IPlatformVersionPatcher>();
        var service = new VersionPatchingService(emptyPatchers);

        // Act
        var act = async () => await service.PatchAsync(null);

        // Assert - Should complete without throwing
        await act.Should().NotThrowAsync();
    }
}
