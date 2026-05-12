#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionDataProviderTests
{
    private static BiblePublicationTrackListViewItemModel TrackVm(string code) =>
        new(new BiblePublicationTrack { TrackCode = code, Title = code });

    [Fact]
    public void SetSelectedTrack_returns_early_when_current_null()
    {
        var tracks = new ObservableCollection<BiblePublicationTrackListViewItemModel> { TrackVm("1") };
        BiblePublicationTrackListViewItemModel? chosen = null;

        TrackSelectionDataProvider.SetSelectedTrack(null, tracks, null, x => chosen = x);

        Assert.Null(chosen);
    }

    [Fact]
    public void SetSelectedTrack_clears_previous_selection_and_selects_matching_track()
    {
        var tracks = new ObservableCollection<BiblePublicationTrackListViewItemModel> { TrackVm("a"), TrackVm("b") };
        tracks[0].IsSelected = true;
        var current = new BiblePublicationSchedule { TrackCode = "b" };
        BiblePublicationTrackListViewItemModel? chosen = null;

        TrackSelectionDataProvider.SetSelectedTrack(current, tracks, tracks[0], x => chosen = x);

        Assert.Same(tracks[1], chosen);
        Assert.True(tracks[1].IsSelected);
        Assert.False(tracks[0].IsSelected);
    }

    [Fact]
    public void SetSelectedTrack_leaves_selection_unchanged_when_track_code_missing_from_collection()
    {
        var tracks = new ObservableCollection<BiblePublicationTrackListViewItemModel> { TrackVm("x") };
        var current = new BiblePublicationSchedule { TrackCode = "y" };
        BiblePublicationTrackListViewItemModel? chosen = null;

        TrackSelectionDataProvider.SetSelectedTrack(current, tracks, null, x => chosen = x);

        Assert.Null(chosen);
        Assert.False(tracks[0].IsSelected);
    }
}
