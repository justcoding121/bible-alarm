#nullable enable

using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

namespace Bible.Alarm.Tests;

public sealed class MusicCommandInitializerTests
{
    [Fact]
    public void Ctor_accepts_deps_record()
    {
        var deps = new MusicSelectionContainerViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var sut = new MusicCommandInitializer(deps);
        Assert.NotNull(sut);
    }
}
