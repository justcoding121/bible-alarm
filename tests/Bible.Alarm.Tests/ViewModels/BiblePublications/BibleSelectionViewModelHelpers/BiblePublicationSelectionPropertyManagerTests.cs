#nullable enable

using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionPropertyManagerTests
{
    [Fact]
    public void Ctor_accepts_state_and_handlers()
    {
        var sut = new BiblePublicationSelectionPropertyManager(null!, null!, null!);
        Assert.NotNull(sut);
    }
}
