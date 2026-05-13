#nullable enable

using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new HomeStateChangeHandlerDeps(null!, null!, null!);
        var b = new HomeStateChangeHandlerDeps(a.Logger, a.DataPreparer, a.ViewModelManager);
        Assert.Equal(a, b);
    }
}
