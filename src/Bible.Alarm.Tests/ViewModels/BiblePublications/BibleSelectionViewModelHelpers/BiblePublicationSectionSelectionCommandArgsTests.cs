#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionSelectionCommandArgsTests
{
    [Fact]
    public void SectionSelectionSelectors_invoke_delegates_and_return_collections()
    {
        var pubs = new ObservableCollection<PublicationListViewItemModel>();
        var map = new Dictionary<string, PublicationListViewItemModel>();

        BiblePublicationSchedule? bible = null;
        LanguageListViewItemModel? language = null;

        var sels = new SectionSelectionSelectors(
            () => language,
            () => pubs,
            () => map,
            () => bible);

        Assert.Null(sels.GetCurrentLanguage());
        Assert.Same(pubs, sels.GetPublications());
        Assert.Same(map, sels.GetPublicationVMsMapping());
        Assert.Null(sels.GetCurrent());
    }

    [Fact]
    public void SectionSelectionUiBindings_invoke_actions()
    {
        bool? progress = null;
        double? pct = null;
        string? text = null;
        bool? busy = null;

        var bindings = new SectionSelectionUiBindings(
            v => progress = v,
            p => pct = p,
            t => text = t,
            v => busy = v);

        bindings.SetShowProgress(true);
        bindings.SetProgressPercent(0.25);
        bindings.SetProgressText("loading");
        bindings.SetIsBusy(false);

        Assert.True(progress);
        Assert.Equal(0.25, pct);
        Assert.Equal("loading", text);
        Assert.False(busy);
    }
}
