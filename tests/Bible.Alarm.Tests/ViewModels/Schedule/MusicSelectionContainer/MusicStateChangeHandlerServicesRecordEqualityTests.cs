#nullable enable

using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

namespace Bible.Alarm.Tests;

public sealed class MusicStateChangeHandlerServicesRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new MusicStateChangeHandlerServices(null!, null!, null!, null!, null!);
        var b = new MusicStateChangeHandlerServices(
            a.Logger,
            a.State,
            a.Dispatcher,
            a.Mapper,
            a.ServiceProvider);

        Assert.Equal(a, b);
    }
}
