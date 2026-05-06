#nullable enable

using System.IO;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class FluxorActionSmokeTests
{
    [Fact]
    public void BackAction_holds_disposable_view_model()
    {
        using var vm = new MemoryStream();
        var sut = new BackAction(vm);

        Assert.Same(vm, sut.CurrentViewModel);
    }

    [Fact]
    public void ResetScheduleStateAction_instantiates()
    {
        var sut = new ResetScheduleStateAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void ResetContainerReadinessAction_instantiates()
    {
        var sut = new ResetContainerReadinessAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void MusicSectionSelectedAction_holds_music_state()
    {
        var music = new MusicStateItem { Id = 10 };
        var sut = new MusicSectionSelectedAction(music);
        Assert.Same(music, sut.CurrentMusic);
    }

    [Fact]
    public void MusicSelectionAction_holds_music_state()
    {
        var music = new MusicStateItem { Id = 11 };
        var sut = new MusicSelectionAction(music);
        Assert.Same(music, sut.CurrentMusic);
    }

    [Fact]
    public void MusicTrackSelectionAction_holds_music_state()
    {
        var music = new MusicStateItem { Id = 12 };
        var sut = new MusicTrackSelectionAction(music);
        Assert.Same(music, sut.CurrentMusic);
    }

    [Fact]
    public void Music_TrackSelectedAction_holds_music_state()
    {
        var music = new MusicStateItem { Id = 13 };
        var sut = new Bible.Alarm.Stores.Actions.Music.TrackSelectedAction(music);
        Assert.Same(music, sut.CurrentMusic);
    }

    [Fact]
    public void Bible_TrackSelectedAction_holds_bible_state()
    {
        var bible = new BiblePublicationStateItem { Id = 20 };
        var sut = new Bible.Alarm.Stores.Actions.BiblePublications.TrackSelectedAction(bible);
        Assert.Same(bible, sut.CurrentBiblePublicationSchedule);
    }

    [Fact]
    public void CategorySelectionAction_holds_category_fields()
    {
        var sut = new CategorySelectionAction(5, "Cat", "en", null);

        Assert.Equal(5, sut.CategoryId);
        Assert.Equal("Cat", sut.CategoryName);
        Assert.Equal("en", sut.PreviousLanguageCode);
        Assert.Null(sut.PreviousScheduleSnapshot);
    }

    [Fact]
    public void BiblePublicationSectionSelectionAction_holds_state()
    {
        var bible = new BiblePublicationStateItem { Id = 30 };
        var sut = new BiblePublicationSectionSelectionAction(bible);
        Assert.Same(bible, sut.CurrentBiblePublicationSchedule);
    }

    [Fact]
    public void SetHomePageOverlayAction_init_IsVisible()
    {
        var sut = new SetHomePageOverlayAction { IsVisible = true };
        Assert.True(sut.IsVisible);
    }

    [Fact]
    public void SetSchedulePageOverlayAction_init_IsVisible()
    {
        var sut = new SetSchedulePageOverlayAction { IsVisible = false };
        Assert.False(sut.IsVisible);
    }

    [Fact]
    public void PlaybackStoppedAction_instantiates()
    {
        var sut = new PlaybackStoppedAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void SetAutoAdvancingAction_holds_flag()
    {
        var sut = new SetAutoAdvancingAction(true);
        Assert.True(sut.IsAutoAdvancing);
    }
}
