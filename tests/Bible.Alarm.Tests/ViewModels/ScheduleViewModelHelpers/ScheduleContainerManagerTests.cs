#nullable enable

using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScheduleContainerManagerTests
{
    private sealed class FakeScheduleContainerService : IScheduleContainerService
    {
        public IServiceProvider? CapturedServiceProvider { get; private set; }

        public Task InitializeContainersAsync(IServiceProvider serviceProvider,
            Action<BiblePublicationSelectionContainerViewModel, MusicSelectionContainerViewModel,
                NumberOfTrackContainerViewModel, ScheduleDetailsContainerViewModel,
                AlarmSettingsContainerViewModel> onContainersReady)
        {
            CapturedServiceProvider = serviceProvider;
            onContainersReady(null!, null!, null!, null!, null!);
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public async Task InitializeContainerViewModelsAsync_forwards_to_schedule_container_service()
    {
        var containers = new FakeScheduleContainerService();
        var services = new EmptyServiceProvider();
        var sut = new ScheduleContainerManager(containers, services);

        var invoked = false;
        await sut.InitializeContainerViewModelsAsync((b, m, n, d, a) =>
        {
            invoked = true;
            Assert.Null(b);
            Assert.Null(m);
            Assert.Null(n);
            Assert.Null(d);
            Assert.Null(a);
        });

        Assert.Same(services, containers.CapturedServiceProvider);
        Assert.True(invoked);
    }
}
