#nullable enable

using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new ScheduleListItemViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new ScheduleListItemViewModelDeps(
            a.Logger,
            a.PlaybackService,
            a.StopPlaybackService,
            a.ScheduleStateService,
            a.ApplicationState,
            a.PlaybackState,
            a.Dispatcher,
            a.Mapper,
            a.CategoryNameService);

        Assert.Equal(a, b);
    }
}
