using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests;

public class ProgramTests
{
    private readonly IFixture _fixture = new Fixture();

    [Fact]
    public void Main_WithValidServices_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var versionPatchingServiceMock = new Mock<IVersionPatchingService>();

        versionPatchingServiceMock.Setup(x => x.PatchAllPlatformsAsync())
            .Returns(Task.CompletedTask);

        services.AddSingleton(versionPatchingServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var action = async () =>
        {
            var versionPatchingService = serviceProvider.GetRequiredService<IVersionPatchingService>();
            await versionPatchingService.PatchAllPlatformsAsync();
        };

        action.Should().NotThrowAsync();
    }

    [Fact]
    public void Main_WithServiceFailure_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();
        var versionPatchingServiceMock = new Mock<IVersionPatchingService>();

        versionPatchingServiceMock.Setup(x => x.PatchAllPlatformsAsync())
            .ThrowsAsync(new Exception("Patching failed"));

        services.AddSingleton(versionPatchingServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var action = async () =>
        {
            var versionPatchingService = serviceProvider.GetRequiredService<IVersionPatchingService>();
            await versionPatchingService.PatchAllPlatformsAsync();
        };

        action.Should().ThrowAsync<Exception>().WithMessage("Patching failed");
    }
}
