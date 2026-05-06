#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackPropertyManagerTests
{
    private static MusicTrackListViewItemModel Track(string code) =>
        new(new MusicTrack { TrackCode = code, Title = $"t-{code}" });

    [Fact]
    public void Defaults_busy_true_and_empty_track_collection()
    {
        var sut = new MusicTrackPropertyManager();

        Assert.True(sut.IsBusy);
        Assert.Empty(sut.Tracks);
        Assert.Null(sut.SelectedTrack);
    }

    [Fact]
    public void IsBusy_setter_updates_value()
    {
        var sut = new MusicTrackPropertyManager();

        sut.IsBusy = false;

        Assert.False(sut.IsBusy);
    }

    [Fact]
    public void SelectedTrack_clears_previous_selection_and_marks_new_track_selected()
    {
        var sut = new MusicTrackPropertyManager();
        var a = Track("1");
        var b = Track("2");
        a.IsSelected = true;
        a.Repeat = true;

        sut.SelectedTrack = a;
        Assert.True(a.IsSelected);
        sut.SelectedTrack = b;

        Assert.False(a.IsSelected);
        Assert.False(a.Repeat);
        Assert.True(b.IsSelected);
        Assert.Same(b, sut.SelectedTrack);
    }

    [Fact]
    public void SelectedTrack_null_clears_flags_on_previous_selection()
    {
        var sut = new MusicTrackPropertyManager();
        var a = Track("1");
        a.Repeat = true;
        sut.SelectedTrack = a;
        Assert.True(a.IsSelected);

        sut.SelectedTrack = null;

        Assert.Null(sut.SelectedTrack);
        Assert.False(a.IsSelected);
        Assert.False(a.Repeat);
    }

    [Fact]
    public void SetSelectedTrack_sets_field_without_toggle_side_effects()
    {
        var sut = new MusicTrackPropertyManager();
        var track = Track("x");

        sut.SetSelectedTrack(track);

        Assert.Same(track, sut.SelectedTrack);
        Assert.False(track.IsSelected);
    }

    [Fact]
    public void Tracks_property_round_trips_collection()
    {
        var sut = new MusicTrackPropertyManager();
        var coll = new ObservableCollection<MusicTrackListViewItemModel>([Track("a"), Track("b")]);

        sut.Tracks = coll;

        Assert.Same(coll, sut.Tracks);
    }
}
