using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests;

public class ProgramTests
{
    private readonly IFixture fixture = new Fixture();

    [Fact]
    public void Main_WithValidServices_ShouldExecuteSuccessfully()
    {
        var services = new ServiceCollection();
        var versionPatchingServiceMock = new Mock<IVersionPatchingService>();

        versionPatchingServiceMock.Setup(x => x.PatchAsync(null))
            .Returns(Task.CompletedTask);

        services.AddSingleton(versionPatchingServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var action = async () =>
        {
            var versionPatchingService = serviceProvider.GetRequiredService<IVersionPatchingService>();
            await versionPatchingService.PatchAsync(null);
        };

        action.Should().NotThrowAsync();
    }

    [Fact]
    public void Main_WithServiceFailure_ShouldThrowException()
    {
        var services = new ServiceCollection();
        var versionPatchingServiceMock = new Mock<IVersionPatchingService>();

        versionPatchingServiceMock.Setup(x => x.PatchAsync(null))
            .ThrowsAsync(new Exception("Patching failed"));

        services.AddSingleton(versionPatchingServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var action = async () =>
        {
            var versionPatchingService = serviceProvider.GetRequiredService<IVersionPatchingService>();
            await versionPatchingService.PatchAsync(null);
        };

        action.Should().ThrowAsync<Exception>().WithMessage("Patching failed");
    }
}
