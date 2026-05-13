#nullable enable

using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

namespace Bible.Alarm.Tests;

public sealed class AnimationManagerTests
{
    [Fact]
    public void Ctor_accepts_views()
    {
        var sut = new AnimationManager(null!, null!);

        Assert.NotNull(sut);
        Assert.False(sut.IsAnimating);
    }
}
