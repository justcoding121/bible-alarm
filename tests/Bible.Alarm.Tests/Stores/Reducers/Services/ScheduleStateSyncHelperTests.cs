#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateSyncHelperTests
{
    [Fact]
    public void HasValidBiblePublicationProperties_false_when_code_or_track_missing()
    {
        Assert.False(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = "",
            BiblePublicationTrackCode = "1",
            BiblePublicationLanguageCode = "E",
        }));

        Assert.False(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationTrackCode = " ",
            BiblePublicationLanguageCode = "E",
        }));
    }

    [Fact]
    public void HasValidBiblePublicationProperties_false_when_language_required_but_missing()
    {
        Assert.False(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationLanguageCode = "",
            BiblePublicationSectionCode = "37",
            BiblePublicationTrackCode = "1",
        }));
    }

    [Fact]
    public void HasValidBiblePublicationProperties_true_for_music_flag_pub_without_language()
    {
        Assert.True(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.MediatorCategoryKeyChildrenSongs,
            BiblePublicationTrackCode = "1",
            BiblePublicationLanguageCode = "",
        }));
    }

    [Fact]
    public void HasValidBiblePublicationProperties_false_when_section_structure_requires_section()
    {
        Assert.False(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationLanguageCode = "E",
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = "1",
        }));
    }

    [Fact]
    public void HasValidBiblePublicationProperties_true_when_section_structure_and_section_present()
    {
        Assert.True(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationLanguageCode = "E",
            BiblePublicationSectionCode = "37",
            BiblePublicationTrackCode = "1",
        }));
    }

    [Fact]
    public void HasValidBiblePublicationProperties_true_for_flat_publication_without_section()
    {
        Assert.True(ScheduleStateSyncHelper.HasValidBiblePublicationProperties(new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.MediatorPublicationCodeVODBibleTeachings,
            BiblePublicationLanguageCode = "E",
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = "1",
        }));
    }

    [Fact]
    public void HasValidMusicProperties_requires_publication_and_track_codes()
    {
        Assert.False(ScheduleStateSyncHelper.HasValidMusicProperties(new ScheduleStateItem
        {
            MusicPublicationCode = "",
            MusicTrackCode = "t",
        }));

        Assert.True(ScheduleStateSyncHelper.HasValidMusicProperties(new ScheduleStateItem
        {
            MusicPublicationCode = "pub",
            MusicTrackCode = "t",
        }));
    }

    [Fact]
    public void UpdateCurrentScheduleIfMatches_returns_existing_when_ids_differ()
    {
        var current = new ScheduleStateItem { Id = 1 };
        var action = new ScheduleStateItem { Id = 2 };
        var state = new ApplicationState([], currentSchedule: current);

        var result = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action);

        Assert.Same(current, result);
    }

    [Fact]
    public void UpdateCurrentScheduleIfMatches_returns_same_instance_when_equivalent()
    {
        var current = new ScheduleStateItem { Id = 5 };
        var action = new ScheduleStateItem { Id = 5 };
        var state = new ApplicationState([], currentSchedule: current);

        var result = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action);

        Assert.Same(current, result);
    }

    [Fact]
    public void UpdateCurrentScheduleIfMatches_returns_clone_when_name_changes()
    {
        var current = new ScheduleStateItem { Id = 5, Name = "Old" };
        var action = new ScheduleStateItem { Id = 5, Name = "New" };
        var state = new ApplicationState([], currentSchedule: current);

        var result = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action);

        Assert.NotNull(result);
        Assert.NotSame(current, result);
        Assert.Equal("New", result.Name);
        Assert.Equal(5, result.Id);
    }

    [Fact]
    public void UpdateCurrentScheduleIfMatches_preserves_days_of_week_and_time_when_action_cleared_them()
    {
        var current = new ScheduleStateItem
        {
            Id = 5,
            Name = "Wake",
            DaysOfWeek = WeekDays.Monday,
            Hour = 6,
            Minute = 30,
            Second = 45,
        };
        var action = new ScheduleStateItem
        {
            Id = 5,
            Name = "Wake renamed",
            DaysOfWeek = 0,
            Hour = 0,
            Minute = 0,
            Second = 0,
        };
        var state = new ApplicationState([], currentSchedule: current);

        var result = ScheduleStateSyncHelper.UpdateCurrentScheduleIfMatches(state, action);

        Assert.NotNull(result);
        Assert.NotSame(current, result);
        Assert.Equal("Wake renamed", result.Name);
        Assert.Equal(WeekDays.Monday, result.DaysOfWeek);
        Assert.Equal(6, result.Hour);
        Assert.Equal(30, result.Minute);
        Assert.Equal(45, result.Second);
    }

    [Fact]
    public void SyncBiblePublicationScheduleIfNeeded_returns_null_when_flag_off_or_invalid()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationLanguageCode = "E",
            BiblePublicationSectionCode = "37",
            BiblePublicationTrackCode = "1",
        };

        var skipFlag = new UpdateScheduleFromViewModelAction(schedule, biblePublicationUpdated: false);
        Assert.Null(ScheduleStateSyncHelper.SyncBiblePublicationScheduleIfNeeded(skipFlag, schedule));

        var skipVm = new UpdateScheduleFromViewModelAction(schedule);
        Assert.Null(ScheduleStateSyncHelper.SyncBiblePublicationScheduleIfNeeded(skipVm, updatedCurrentSchedule: null));
    }

    [Fact]
    public void SyncBiblePublicationScheduleIfNeeded_maps_schedule_when_valid()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 9,
            BiblePublicationScheduleId = 100,
            BiblePublicationCategoryId = 3,
            BiblePublicationCategoryName = "Cat",
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionCode = "37",
            BiblePublicationTrackCode = "12",
            BiblePublicationLanguageName = "English",
            BiblePublicationLanguageDirection = "ltr",
            BiblePublicationName = "NWT",
            BiblePublicationSectionName = "Matthew",
            BiblePublicationTrackTitle = "Track",
        };

        var action = new UpdateScheduleFromViewModelAction(schedule);
        var item = ScheduleStateSyncHelper.SyncBiblePublicationScheduleIfNeeded(action, schedule);

        Assert.NotNull(item);
        Assert.Equal(100, item.Id);
        Assert.Equal(9, item.AlarmScheduleId);
        Assert.Equal("12", item.TrackCode);
        Assert.Equal("37", item.SectionCode);
        Assert.Equal("English", item.LanguageName);
    }

    [Fact]
    public void SyncMusicIfNeeded_returns_music_when_valid()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 2,
            MusicId = 50,
            MusicPublicationCode = "PUB",
            MusicLanguageCode = "E",
            MusicSectionCode = "S",
            MusicTrackCode = "T",
            MusicRepeat = true,
            MusicLanguageName = "En",
            MusicLanguageDirection = "ltr",
            MusicPublicationName = "PubName",
            MusicSectionName = "Sec",
            MusicTrackName = "Trk",
        };

        var action = new UpdateScheduleFromViewModelAction(schedule, musicUpdated: true);
        var music = ScheduleStateSyncHelper.SyncMusicIfNeeded(action, schedule);

        Assert.NotNull(music);
        Assert.Equal(50, music.Id);
        Assert.Equal("PUB", music.PublicationCode);
        Assert.Equal("T", music.TrackCode);
        Assert.True(music.Repeat);
    }

    [Fact]
    public void CreateBiblePublicationScheduleFromCurrent_throws_when_track_missing_or_section_required()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ScheduleStateSyncHelper.CreateBiblePublicationScheduleFromCurrent(new ScheduleStateItem
            {
                BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                BiblePublicationLanguageCode = "E",
                BiblePublicationSectionCode = "37",
                BiblePublicationTrackCode = "",
            }));

        Assert.Throws<InvalidOperationException>(() =>
            ScheduleStateSyncHelper.CreateBiblePublicationScheduleFromCurrent(new ScheduleStateItem
            {
                BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                BiblePublicationLanguageCode = "E",
                BiblePublicationSectionCode = "",
                BiblePublicationTrackCode = "1",
            }));
    }

    [Fact]
    public void CreateMusicFromCurrent_throws_when_track_missing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ScheduleStateSyncHelper.CreateMusicFromCurrent(new ScheduleStateItem
            {
                MusicPublicationCode = "PUB",
                MusicTrackCode = "",
            }));
    }
}
