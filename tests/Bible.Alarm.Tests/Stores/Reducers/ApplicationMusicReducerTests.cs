#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class ApplicationMusicReducerTests
{
    private static ScheduleStateItem BaseSchedule(string musicPublicationCode = "iam") =>
        new()
        {
            Id = 1,
            Name = "S",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicPublicationCode = musicPublicationCode,
            MusicLanguageCode = "MY",
            MusicLanguageName = "KeepMe",
            MusicTrackCode = "01",
        };

    private static MusicStateItem VocalSelection() =>
        new()
        {
            PublicationCode = "pub",
            LanguageCode = "E",
            SectionCode = "s1",
            TrackCode = "t1",
            Repeat = true,
            LanguageName = "English",
            LanguageDirection = "ltr",
            PublicationName = "Pub",
            SectionName = "Sec",
            TrackName = "Track",
        };

    [Fact]
    public void OnMusicSelection_returns_same_state_instance()
    {
        var state = new ApplicationState([]);
        var music = new MusicStateItem { PublicationCode = "x" };
        Assert.Same(state, ApplicationMusicReducer.OnMusicSelection(state, new MusicSelectionAction(music)));
    }

    [Fact]
    public void OnMusicPublicationSelection_returns_same_state_instance()
    {
        var state = new ApplicationState([]);
        var music = new MusicStateItem { PublicationCode = "x" };
        Assert.Same(state, ApplicationMusicReducer.OnMusicPublicationSelection(state, new MusicPublicationSelectionAction(music)));
    }

    [Fact]
    public void OnMusicTrackSelection_returns_same_state_instance()
    {
        var state = new ApplicationState([]);
        var music = new MusicStateItem { PublicationCode = "x" };
        Assert.Same(state, ApplicationMusicReducer.OnMusicTrackSelection(state, new MusicTrackSelectionAction(music)));
    }

    [Fact]
    public void OnMusicTrackSelected_without_CurrentSchedule_writes_null_current_via_factory()
    {
        var prior = new ApplicationState([]);
        var next = ApplicationMusicReducer.OnMusicTrackSelected(prior, new TrackSelectedAction(VocalSelection()));

        Assert.Null(next.CurrentSchedule);
        Assert.Same(prior.Schedules, next.Schedules);
    }

    [Fact]
    public void OnMusicTrackSelected_clones_schedule_and_updates_flattened_music()
    {
        var current = BaseSchedule(musicPublicationCode: "iam");
        var prior = new ApplicationState([], currentSchedule: current);
        var actionMusic = VocalSelection();

        var next = ApplicationMusicReducer.OnMusicTrackSelected(prior, new TrackSelectedAction(actionMusic));
        var u = next.CurrentSchedule!;

        Assert.NotSame(current, u);
        Assert.Equal(actionMusic.PublicationCode, u.MusicPublicationCode);
        Assert.Equal(actionMusic.LanguageCode, u.MusicLanguageCode);
        Assert.Equal(actionMusic.SectionCode, u.MusicSectionCode);
        Assert.Equal(actionMusic.TrackCode, u.MusicTrackCode);
        Assert.Equal(actionMusic.Repeat, u.MusicRepeat);
        Assert.Equal(actionMusic.LanguageName, u.MusicLanguageName);
        Assert.Equal(actionMusic.LanguageDirection, u.MusicLanguageDirection);
        Assert.Equal(actionMusic.PublicationName, u.MusicPublicationName);
        Assert.Equal(actionMusic.SectionName, u.MusicSectionName);
        Assert.Equal(actionMusic.TrackName, u.MusicTrackName);
    }

    [Fact]
    public void OnMusicTrackSelected_melody_preserves_schedule_language_when_action_has_no_language_code()
    {
        var current = BaseSchedule(musicPublicationCode: "iam");
        current.MusicLanguageCode = "MY";
        var prior = new ApplicationState([], currentSchedule: current);

        var music = new MusicStateItem
        {
            PublicationCode = "osg",
            LanguageCode = null,
            SectionCode = "a",
            TrackCode = "b",
            PublicationName = "P",
            SectionName = null,
            TrackName = "T",
        };

        var next = ApplicationMusicReducer.OnMusicTrackSelected(prior, new TrackSelectedAction(music));
        Assert.Equal("MY", next.CurrentSchedule!.MusicLanguageCode);
    }

    [Fact]
    public void OnMusicTrackSelected_melody_uses_app_default_language_when_schedule_had_none()
    {
        var current = BaseSchedule(musicPublicationCode: "iam");
        current.MusicLanguageCode = null;
        var prior = new ApplicationState([], currentSchedule: current);

        var music = new MusicStateItem { PublicationCode = "iam", TrackCode = "1", LanguageCode = null };
        var next = ApplicationMusicReducer.OnMusicTrackSelected(prior, new TrackSelectedAction(music));

        Assert.Equal(AppConstants.Media.DefaultLanguageCode, next.CurrentSchedule!.MusicLanguageCode);
    }

    [Fact]
    public void OnMusicTrackSelected_preserves_existing_language_display_when_action_names_empty()
    {
        var current = BaseSchedule(musicPublicationCode: "iam");
        current.MusicLanguageName = "Saved";
        current.MusicLanguageDirection = "rtl";
        var prior = new ApplicationState([], currentSchedule: current);

        var music = new MusicStateItem
        {
            PublicationCode = "pub",
            LanguageCode = "",
            TrackCode = "1",
            LanguageName = null,
            LanguageDirection = null,
            PublicationName = "Pub",
            TrackName = "Tr",
        };

        var next = ApplicationMusicReducer.OnMusicTrackSelected(prior, new TrackSelectedAction(music));

        Assert.Equal("Saved", next.CurrentSchedule!.MusicLanguageName);
        Assert.Equal("rtl", next.CurrentSchedule.MusicLanguageDirection);
    }

    [Fact]
    public void OnMusicSectionSelected_updates_like_track_selected_when_schedule_present()
    {
        var prior = new ApplicationState([], currentSchedule: BaseSchedule());
        var music = VocalSelection();

        var next = ApplicationMusicReducer.OnMusicSectionSelected(prior, new MusicSectionSelectedAction(music));
        Assert.Equal(music.PublicationCode, next.CurrentSchedule!.MusicPublicationCode);
        Assert.Equal(music.SectionCode, next.CurrentSchedule.MusicSectionCode);
    }

    [Fact]
    public void OnMusicSectionSelected_without_CurrentSchedule_leaves_null()
    {
        var prior = new ApplicationState([]);
        var next = ApplicationMusicReducer.OnMusicSectionSelected(prior, new MusicSectionSelectedAction(VocalSelection()));

        Assert.Null(next.CurrentSchedule);
    }

    [Fact]
    public void OnMusicTrackSelected_with_null_action_music_keeps_current_schedule_unchanged()
    {
        var current = BaseSchedule();
        var prior = new ApplicationState([], currentSchedule: current);

        var next = ApplicationMusicReducer.OnMusicTrackSelected(prior, new TrackSelectedAction(null!));

        Assert.Same(current, next.CurrentSchedule);
    }

    [Fact]
    public void OnMusicSectionSelected_with_null_action_music_keeps_current_schedule_unchanged()
    {
        var current = BaseSchedule();
        var prior = new ApplicationState([], currentSchedule: current);

        var next = ApplicationMusicReducer.OnMusicSectionSelected(prior, new MusicSectionSelectedAction(null!));

        Assert.Same(current, next.CurrentSchedule);
    }
}
