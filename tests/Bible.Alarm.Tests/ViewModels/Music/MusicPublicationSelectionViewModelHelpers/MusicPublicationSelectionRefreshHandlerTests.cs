#nullable enable

using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionRefreshHandlerTests
{
    [Fact]
    public void Ctor_accepts_collaborators()
    {
        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();
        LanguageListViewItemModel? current = null;
        var sut = new MusicPublicationSelectionRefreshHandler(
            null!,
            null!,
            () => languages,
            () => current,
            lang => current = lang,
            _ => { },
            _ => { },
            _ => { });
        Assert.NotNull(sut);
    }
}
