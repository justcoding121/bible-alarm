#nullable enable

using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScheduleCommandServiceDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new ScheduleCommandServiceDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new ScheduleCommandServiceDeps(
            a.Logger,
            a.Dispatcher,
            a.NavigationService,
            a.ScheduleSaveService,
            a.PlaybackService,
            a.NotificationService,
            a.ToastService,
            a.Mapper,
            a.State);

        Assert.Equal(a, b);
    }
}
