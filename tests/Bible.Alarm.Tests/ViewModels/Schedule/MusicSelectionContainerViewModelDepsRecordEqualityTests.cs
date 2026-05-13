#nullable enable

using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Tests;

public sealed class MusicSelectionContainerViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MusicSelectionContainerViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new MusicSelectionContainerViewModelDeps(
            a.Logger,
            a.NavigationService,
            a.ScheduleSelectionService,
            a.MediaService,
            a.ApplicationState,
            a.Dispatcher,
            a.Mapper,
            a.ServiceProvider,
            a.ToastService);

        Assert.Equal(a, b);
    }
}
