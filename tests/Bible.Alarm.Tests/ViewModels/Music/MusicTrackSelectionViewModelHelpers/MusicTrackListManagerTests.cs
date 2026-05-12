#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackListManagerTests
{
    private static MusicTrackListViewItemModel Vm(string code) =>
        new(new MusicTrack { TrackCode = code, Title = code });

    private static MusicTrackListManager CreateSut() =>
        new(TestLogging.CreateLogger(), new IdleCatalogMediaService());

    [Fact]
    public void SetSelectedTrack_returns_without_invoking_callback_when_alarm_music_null()
    {
        MusicTrackListViewItemModel? chosen = null;
        var tracks = new ObservableCollection<MusicTrackListViewItemModel> { Vm("1") };

        MusicTrackListManager.SetSelectedTrack(null, tracks, _ => chosen = _);

        Assert.Null(chosen);
        Assert.False(tracks[0].IsSelected);
    }

    [Fact]
    public void SetSelectedTrack_selects_matching_track_and_assigns_repeat_from_alarm()
    {
        MusicTrackListViewItemModel? chosen = null;
        var a = Vm("10");
        var b = Vm("20");
        var tracks = new ObservableCollection<MusicTrackListViewItemModel> { a, b };
        var alarm = new AlarmMusic { TrackCode = "20", Repeat = true };

        MusicTrackListManager.SetSelectedTrack(alarm, tracks, x => chosen = x);

        Assert.Same(b, chosen);
        Assert.True(b.IsSelected);
        Assert.True(b.Repeat);
        Assert.False(a.IsSelected);
    }

    [Fact]
    public void SubscribeToTrackEvents_clears_repeat_on_other_items_when_one_turns_repeat_on()
    {
        var sut = CreateSut();
        var a = Vm("a");
        var b = Vm("b");
        a.Repeat = true;
        var tracks = new ObservableCollection<MusicTrackListViewItemModel> { a, b };
        sut.SubscribeToTrackEvents(a, tracks);
        sut.SubscribeToTrackEvents(b, tracks);

        b.Repeat = true;

        Assert.True(b.Repeat);
        Assert.False(a.Repeat);
    }

    [Fact]
    public void TeardownCollectionChangedHandler_unsubscribes_repeat_handlers_for_all_tracks()
    {
        var sut = CreateSut();
        var a = Vm("a");
        var b = Vm("b");
        var tracks = new ObservableCollection<MusicTrackListViewItemModel> { a, b };
        sut.SetupCollectionChangedHandler(tracks);
        sut.SubscribeToTrackEvents(a, tracks);
        sut.SubscribeToTrackEvents(b, tracks);

        sut.TeardownCollectionChangedHandler();

        a.Repeat = true;
        b.Repeat = true;

        Assert.True(a.Repeat);
        Assert.True(b.Repeat);
    }
}
