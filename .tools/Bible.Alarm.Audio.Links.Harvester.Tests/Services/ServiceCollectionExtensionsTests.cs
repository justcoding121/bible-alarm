using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests.Services;

public class ServiceCollectionExtensionsTests
{
    private readonly IFixture _fixture;

    public ServiceCollectionExtensionsTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void AddHarvesterServices_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHarvesterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IBibleHarvesterService>().Should().NotBeNull();
        serviceProvider.GetService<IMusicHarvesterService>().Should().NotBeNull();
        serviceProvider.GetService<IIndexService>().Should().NotBeNull();
        serviceProvider.GetService<ICloudPublishingService>().Should().NotBeNull();
        serviceProvider.GetService<IFileSystemService>().Should().NotBeNull();
        serviceProvider.GetService<IConfigurationService>().Should().NotBeNull();
        serviceProvider.GetService<IHarvestingOrchestratorService>().Should().NotBeNull();
    }

    [Fact]
    public void AddHarvesterServices_ShouldRegisterServicesAsSingletons()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHarvesterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var bibleHarvester1 = serviceProvider.GetService<IBibleHarvesterService>();
        var bibleHarvester2 = serviceProvider.GetService<IBibleHarvesterService>();
        bibleHarvester1.Should().BeSameAs(bibleHarvester2);

        var musicHarvester1 = serviceProvider.GetService<IMusicHarvesterService>();
        var musicHarvester2 = serviceProvider.GetService<IMusicHarvesterService>();
        musicHarvester1.Should().BeSameAs(musicHarvester2);
    }
}
