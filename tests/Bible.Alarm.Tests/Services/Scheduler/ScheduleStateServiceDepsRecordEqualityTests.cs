#nullable enable

using Bible.Alarm.Services.Scheduler;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateServiceDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new ScheduleStateServiceDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new ScheduleStateServiceDeps(
            a.Logger,
            a.AlarmScheduleService,
            a.AlarmService,
            a.NotificationService,
            a.ToastService,
            a.Dispatcher,
            a.NavigationService,
            a.ServiceProvider);

        Assert.Equal(a, b);
    }
}
