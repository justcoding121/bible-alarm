#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMediaCacheServiceTests
{
    private sealed class RecordingMediaCacheSetup : IMediaCacheSetupService
    {
        public List<int> SetupAlarmCacheCalls { get; } = [];

        public Task SetupAlarmCacheAsync(int scheduleId)
        {
            SetupAlarmCacheCalls.Add(scheduleId);
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void SetupMediaCache_skips_non_positive_schedule_ids()
    {
        var setup = new RecordingMediaCacheSetup();
        var sut = new ScheduleMediaCacheService(TestLogging.CreateLogger(), setup, new EmptyServiceProvider());

        sut.SetupMediaCache(0);

        Assert.Empty(setup.SetupAlarmCacheCalls);
    }

    [Fact]
    public async Task SetupMediaCache_runs_alarm_setup_on_background_for_new_schedule()
    {
        var setup = new RecordingMediaCacheSetup();
        var sut = new ScheduleMediaCacheService(TestLogging.CreateLogger(), setup, new EmptyServiceProvider());

        sut.SetupMediaCache(42, isUpdate: false);

        await Task.Delay(300);

        Assert.Equal(42, Assert.Single(setup.SetupAlarmCacheCalls));
    }
}
