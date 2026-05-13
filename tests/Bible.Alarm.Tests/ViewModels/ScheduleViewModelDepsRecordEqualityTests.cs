#nullable enable

using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests;

public sealed class ScheduleViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new ScheduleViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new ScheduleViewModelDeps(
            a.Logger,
            a.ServiceProvider,
            a.Mapper,
            a.ApplicationState,
            a.PlaybackState,
            a.Dispatcher,
            a.ScheduleInitializationService,
            a.ScheduleCommandService,
            a.ScheduleMediaCacheService,
            a.ScheduleContainerService,
            a.ScheduleStateChangeHandler);

        Assert.Equal(a, b);
    }
}
