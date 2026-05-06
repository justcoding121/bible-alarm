#nullable enable

using System.IO;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Schedule;
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

    [Fact]
    public void SetCarPlayScreenAction_instantiates()
    {
        var sut = new SetCarPlayScreenAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void RotateDefaultScheduleAction_instantiates()
    {
        var sut = new RotateDefaultScheduleAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void SetDefaultScheduleMetadataAction_init_properties()
    {
        var sut = new SetDefaultScheduleMetadataAction
        {
            ScheduleId = 3,
            Title = "T",
            Artist = "A",
            Album = "Al",
            ArtworkUrl = "u",
        };

        Assert.Equal(3, sut.ScheduleId);
        Assert.Equal("T", sut.Title);
        Assert.Equal("Al", sut.Album);
    }

    [Fact]
    public void PlaybackNavigationChangedAction_holds_flags()
    {
        var sut = new PlaybackNavigationChangedAction(canPlayNext: true, canPlayPrevious: false);
        Assert.True(sut.CanPlayNext);
        Assert.False(sut.CanPlayPrevious);
    }

    [Fact]
    public void PlaybackStatusChangedAction_holds_status()
    {
        var sut = new PlaybackStatusChangedAction(PlayStatus.Loading);
        Assert.Equal(PlayStatus.Loading, sut.Status);
    }

    [Fact]
    public void PlaybackStartedAction_holds_schedule_id()
    {
        var sut = new PlaybackStartedAction(88);
        Assert.Equal(88, sut.ScheduleId);
    }

    [Fact]
    public void PlaybackMetadataChangedAction_init_properties()
    {
        var sut = new PlaybackMetadataChangedAction { Title = "x", Artist = "y" };
        Assert.Equal("x", sut.Title);
        Assert.Equal("y", sut.Artist);
    }

    [Fact]
    public void PlaybackDurationChangedAction_init_duration()
    {
        var sut = new PlaybackDurationChangedAction { Duration = TimeSpan.FromMinutes(4) };
        Assert.Equal(TimeSpan.FromMinutes(4), sut.Duration);
    }

    [Fact]
    public void PlaybackErrorAction_init_message()
    {
        var sut = new PlaybackErrorAction { ErrorMessage = "e" };
        Assert.Equal("e", sut.ErrorMessage);
    }

    [Fact]
    public void ContainerReadyAction_holds_name()
    {
        var sut = new ContainerReadyAction("ScheduleContent");
        Assert.Equal("ScheduleContent", sut.ContainerName);
    }

    [Fact]
    public void ViewScheduleAction_holds_selected_schedule()
    {
        var sched = new ScheduleStateItem { Id = 50 };
        var sut = new ViewScheduleAction(sched);
        Assert.Same(sched, sut.SelectedSchedule);
    }

    [Fact]
    public void AddScheduleAction_holds_alarm_schedule()
    {
        var db = new AlarmSchedule { Id = 5, Name = "n" };
        var sut = new AddScheduleAction(db);
        Assert.Same(db, sut.Schedule);
    }

    [Fact]
    public void AddScheduleSuccessAction_holds_state_item()
    {
        var item = new ScheduleStateItem { Id = 6 };
        var sut = new AddScheduleSuccessAction(item);
        Assert.Same(item, sut.Schedule);
    }

    [Fact]
    public void CreateScheduleAction_holds_flags()
    {
        var item = new ScheduleStateItem { Id = 7 };
        var sut = new CreateScheduleAction(item, musicUpdated: false, biblePublicationUpdated: true);
        Assert.Same(item, sut.Schedule);
        Assert.False(sut.MusicUpdated);
        Assert.True(sut.BiblePublicationUpdated);
    }

    [Fact]
    public void CreateScheduleSuccessAction_holds_schedule()
    {
        var item = new ScheduleStateItem { Id = 8 };
        var sut = new CreateScheduleSuccessAction(item);
        Assert.Same(item, sut.Schedule);
    }

    [Fact]
    public void CreateScheduleFailureAction_holds_error()
    {
        var item = new ScheduleStateItem { Id = 9 };
        var sut = new CreateScheduleFailureAction(item, "failed");
        Assert.Equal("failed", sut.Error);
        Assert.Same(item, sut.Schedule);
    }

    [Fact]
    public void RemoveScheduleAction_holds_alarm_schedule()
    {
        var db = new AlarmSchedule { Id = 10 };
        var sut = new RemoveScheduleAction(db);
        Assert.Same(db, sut.Schedule);
    }

    [Fact]
    public void DeleteScheduleAction_holds_id()
    {
        var sut = new DeleteScheduleAction(11);
        Assert.Equal(11, sut.ScheduleId);
    }

    [Fact]
    public void UpdateScheduleAction_holds_alarm_schedule()
    {
        var db = new AlarmSchedule { Id = 12 };
        var sut = new UpdateScheduleAction(db);
        Assert.Same(db, sut.Schedule);
    }

    [Fact]
    public void UpdateScheduleSuccessAction_holds_skip_cache_refresh()
    {
        var item = new ScheduleStateItem { Id = 13 };
        var sut = new UpdateScheduleSuccessAction(item, skipCacheRefresh: true);
        Assert.True(sut.SkipCacheRefresh);
        Assert.Same(item, sut.Schedule);
    }

    [Fact]
    public void UpdateScheduleFailureAction_holds_error()
    {
        var item = new ScheduleStateItem { Id = 14 };
        var sut = new UpdateScheduleFailureAction(item, "x");
        Assert.Equal("x", sut.Error);
    }

    [Fact]
    public void UpdateScheduleFromViewModelAction_holds_should_save()
    {
        var item = new ScheduleStateItem { Id = 15 };
        var sut = new UpdateScheduleFromViewModelAction(item, musicUpdated: false, biblePublicationUpdated: false, shouldSave: false);
        Assert.False(sut.ShouldSave);
        Assert.Same(item, sut.Schedule);
    }

    [Fact]
    public void RemoveScheduleSuccessAction_holds_id()
    {
        var sut = new RemoveScheduleSuccessAction(16);
        Assert.Equal(16, sut.ScheduleId);
    }

    [Fact]
    public void DeleteScheduleFailureAction_holds_fields()
    {
        var sched = new ScheduleStateItem { Id = 17 };
        var sut = new DeleteScheduleFailureAction(17, "err", sched);
        Assert.Equal("err", sut.Error);
        Assert.Same(sched, sut.Schedule);
    }

    [Fact]
    public void MusicPublicationSelectionAction_holds_music()
    {
        var m = new MusicStateItem { Id = 18 };
        var sut = new MusicPublicationSelectionAction(m);
        Assert.Same(m, sut.CurrentMusic);
    }

    [Fact]
    public void BiblePublicationSelectionAction_holds_bible_state()
    {
        var b = new BiblePublicationStateItem { Id = 19 };
        var sut = new BiblePublicationSelectionAction(b);
        Assert.Same(b, sut.CurrentBiblePublicationSchedule);
    }
}
