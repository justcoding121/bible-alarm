using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class VersionPatchingServiceTests
{
    private readonly Mock<IPlatformVersionPatcher> _androidPatcherMock;
    private readonly Mock<IPlatformVersionPatcher> _iosPatcherMock;
    private readonly Mock<IPlatformVersionPatcher> _windowsPatcherMock;
    private readonly VersionPatchingService _service;
    private readonly IFixture _fixture;

    public VersionPatchingServiceTests()
    {
        _fixture = new Fixture();
        _androidPatcherMock = new Mock<IPlatformVersionPatcher>();
        _iosPatcherMock = new Mock<IPlatformVersionPatcher>();
        _windowsPatcherMock = new Mock<IPlatformVersionPatcher>();

        var patchers = new List<IPlatformVersionPatcher>
        {
            _androidPatcherMock.Object,
            _iosPatcherMock.Object,
            _windowsPatcherMock.Object
        };

        _service = new VersionPatchingService(patchers);
    }

    [Fact]
    public async Task PatchAllPlatformsAsync_ShouldCallAllPatchers()
    {
        // Arrange
        _androidPatcherMock.Setup(x => x.PlatformName).Returns("Android");
        _iosPatcherMock.Setup(x => x.PlatformName).Returns("iOS");
        _windowsPatcherMock.Setup(x => x.PlatformName).Returns("Windows");

        _androidPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);
        _iosPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);
        _windowsPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.PatchAllPlatformsAsync();

        // Assert
        _androidPatcherMock.Verify(x => x.PatchVersionAsync(), Times.Once);
        _iosPatcherMock.Verify(x => x.PatchVersionAsync(), Times.Once);
        _windowsPatcherMock.Verify(x => x.PatchVersionAsync(), Times.Once);
    }

    [Fact]
    public async Task PatchAllPlatformsAsync_WhenOnePatcherFails_ShouldThrowException()
    {
        // Arrange
        _androidPatcherMock.Setup(x => x.PlatformName).Returns("Android");
        _iosPatcherMock.Setup(x => x.PlatformName).Returns("iOS");
        _windowsPatcherMock.Setup(x => x.PlatformName).Returns("Windows");

        _androidPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);
        _iosPatcherMock.Setup(x => x.PatchVersionAsync()).ThrowsAsync(new Exception("iOS patcher failed"));
        _windowsPatcherMock.Setup(x => x.PatchVersionAsync()).Returns(Task.CompletedTask);

        // Act & Assert
        var action = async () => await _service.PatchAllPlatformsAsync();
        await action.Should().ThrowAsync<Exception>().WithMessage("iOS patcher failed");
    }

    [Fact]
    public async Task PatchAllPlatformsAsync_WithEmptyPatchersList_ShouldCompleteSuccessfully()
    {
        // Arrange
        var emptyPatchers = new List<IPlatformVersionPatcher>();
        var service = new VersionPatchingService(emptyPatchers);

        // Act
        await service.PatchAllPlatformsAsync();

        // Assert - Should complete without throwing
    }
}
