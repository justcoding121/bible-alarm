#nullable enable

using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScrollManagerTests
{
    [Fact]
    public void Ctor_accepts_container()
    {
        var sut = new ScrollManager(null!);

        Assert.NotNull(sut);
    }
}
