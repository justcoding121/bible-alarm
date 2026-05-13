#nullable enable

using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationCommandInitializerTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new BiblePublicationCommandInitializer(
            null!, null!, null!, null!, null!, null!, null!);
        Assert.NotNull(sut);
    }
}
