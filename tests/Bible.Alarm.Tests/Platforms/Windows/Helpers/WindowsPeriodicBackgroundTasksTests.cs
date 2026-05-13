#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.Platforms.Windows.Helpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsPeriodicBackgroundTasksTests
{
    private sealed class RecordingSchedulerService : ISchedulerService
    {
        public int HandleAsyncCallCount { get; private set; }

        public Task<bool> HandleAsync()
        {
            HandleAsyncCallCount++;
            return Task.FromResult(true);
        }

        public Task ProcessScheduledTasksAsync() => Task.CompletedTask;

        public Task RescheduleNextOccurrenceAsync(int scheduleId) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void Dispose_can_be_called_twice_without_throwing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISchedulerService, RecordingSchedulerService>();
        var sp = services.BuildServiceProvider();
        var sut = new WindowsPeriodicBackgroundTasks(TestLogging.CreateLogger(), sp);

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void Start_after_Dispose_is_ignored_without_throwing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISchedulerService, RecordingSchedulerService>();
        var sp = services.BuildServiceProvider();
        var sut = new WindowsPeriodicBackgroundTasks(TestLogging.CreateLogger(), sp);

        sut.Dispose();
        sut.Start();
    }
}
