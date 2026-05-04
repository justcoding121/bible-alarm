#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionPropertyManagerTests
{
    private static BiblePublicationTrackListViewItemModel TrackItem(string code) =>
        new(new BiblePublicationTrack
        {
            TrackCode = code,
            Title = $"Title-{code}",
            BiblePublicationId = 1,
            Publication = null!,
        });

    [Fact]
    public void Defaults_busy_true_and_lazy_empty_tracks()
    {
        var sut = new TrackSelectionPropertyManager();

        Assert.True(sut.IsBusy);
        Assert.Empty(sut.Tracks);
    }

    [Fact]
    public void Tracks_and_selected_track_roundtrip()
    {
        var sut = new TrackSelectionPropertyManager();
        var coll = new ObservableCollection<BiblePublicationTrackListViewItemModel>([TrackItem("1"), TrackItem("2")]);
        var selected = coll[0];

        sut.IsBusy = false;
        sut.Tracks = coll;
        sut.SelectedTrack = selected;

        Assert.False(sut.IsBusy);
        Assert.Same(coll, sut.Tracks);
        Assert.Same(selected, sut.SelectedTrack);
    }
}
