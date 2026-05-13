#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionRefreshHandlerTests
{
    [Fact]
    public void Ctor_stores_logger()
    {
        var sut = new MusicSectionSelectionRefreshHandler(TestLogging.CreateLogger());
        Assert.NotNull(sut);
    }
}
