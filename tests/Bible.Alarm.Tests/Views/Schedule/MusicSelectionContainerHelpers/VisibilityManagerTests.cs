#nullable enable

using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

namespace Bible.Alarm.Tests;

public sealed class VisibilityManagerTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var animation = new AnimationManager(null!, null!);
        var sut = new VisibilityManager(null!, animation);

        Assert.NotNull(sut);
    }
}
