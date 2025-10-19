using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class ServiceCollectionExtensionsTests
{
    private readonly IFixture _fixture = new Fixture();

    [Fact]
    public void AddVersionPatchingServices_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddVersionPatchingServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IVersionService>().Should().NotBeNull();
        serviceProvider.GetService<IFileService>().Should().NotBeNull();
        serviceProvider.GetService<IVersionPatchingService>().Should().NotBeNull();
        
        var platformPatchers = serviceProvider.GetServices<IPlatformVersionPatcher>();
        platformPatchers.Should().HaveCount(3);
        platformPatchers.Should().Contain(p => p.PlatformName == "Android");
        platformPatchers.Should().Contain(p => p.PlatformName == "iOS");
        platformPatchers.Should().Contain(p => p.PlatformName == "Windows");
    }

    [Fact]
    public void AddVersionPatchingServices_ShouldRegisterServicesAsSingletons()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddVersionPatchingServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var versionService1 = serviceProvider.GetService<IVersionService>();
        var versionService2 = serviceProvider.GetService<IVersionService>();
        versionService1.Should().BeSameAs(versionService2);

        var fileService1 = serviceProvider.GetService<IFileService>();
        var fileService2 = serviceProvider.GetService<IFileService>();
        fileService1.Should().BeSameAs(fileService2);
    }
}
