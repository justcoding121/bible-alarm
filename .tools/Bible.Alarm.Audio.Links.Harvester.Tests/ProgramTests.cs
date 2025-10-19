using AutoFixture;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bible.Alarm.Audio.Links.Harvester.Tests;

public class ProgramTests
{
    private readonly IFixture _fixture;

    public ProgramTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void Main_WithValidServices_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var harvestingOrchestratorMock = new Mock<IHarvestingOrchestratorService>();
        
        harvestingOrchestratorMock.Setup(x => x.ExecuteHarvestingAsync())
            .Returns(Task.CompletedTask);

        services.AddSingleton(harvestingOrchestratorMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var action = async () =>
        {
            var harvestingOrchestrator = serviceProvider.GetRequiredService<IHarvestingOrchestratorService>();
            await harvestingOrchestrator.ExecuteHarvestingAsync();
        };

        action.Should().NotThrowAsync();
    }

    [Fact]
    public void Main_WithServiceFailure_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();
        var harvestingOrchestratorMock = new Mock<IHarvestingOrchestratorService>();
        
        harvestingOrchestratorMock.Setup(x => x.ExecuteHarvestingAsync())
            .ThrowsAsync(new Exception("Harvesting failed"));

        services.AddSingleton(harvestingOrchestratorMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var action = async () =>
        {
            var harvestingOrchestrator = serviceProvider.GetRequiredService<IHarvestingOrchestratorService>();
            await harvestingOrchestrator.ExecuteHarvestingAsync();
        };

        action.Should().ThrowAsync<Exception>().WithMessage("Harvesting failed");
    }
}
