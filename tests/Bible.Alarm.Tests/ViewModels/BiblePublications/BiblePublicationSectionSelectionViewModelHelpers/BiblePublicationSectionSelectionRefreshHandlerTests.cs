#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionSelectionRefreshHandlerTests
{
    [Fact]
    public void Ctor_stores_logger()
    {
        var sut = new BiblePublicationSectionSelectionRefreshHandler(TestLogging.CreateLogger());
        Assert.NotNull(sut);
    }
}
