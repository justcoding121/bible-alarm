#nullable enable

using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new HomeViewModelDeps(null!, null!, null!, null!, null!, null!, null!);
        var b = new HomeViewModelDeps(
            a.Logger,
            a.ServiceProvider,
            a.ApplicationState,
            a.PlaybackState,
            a.Dispatcher,
            a.NavigationService,
            a.Mapper);
        Assert.Equal(a, b);
    }
}
