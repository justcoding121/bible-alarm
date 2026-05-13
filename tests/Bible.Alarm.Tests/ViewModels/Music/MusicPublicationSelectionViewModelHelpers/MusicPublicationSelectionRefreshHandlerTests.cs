#nullable enable

using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionRefreshHandlerTests
{
    [Fact]
    public void Ctor_accepts_collaborators()
    {
        var sut = new MusicPublicationSelectionRefreshHandler(null!, null!, null!);
        Assert.NotNull(sut);
    }
}
