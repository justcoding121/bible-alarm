#nullable enable

using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemStateChangeApplierTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new ScheduleListItemStateChangeApplier(null!, null!, null!, null!);
        Assert.NotNull(sut);
    }
}
