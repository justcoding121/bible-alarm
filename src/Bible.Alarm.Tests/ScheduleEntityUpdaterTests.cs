#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEntityUpdaterTests
{
    [Fact]
    public void HasValidMusicProperties_requires_publication_and_track_codes()
    {
        Assert.False(ScheduleEntityUpdater.HasValidMusicProperties(null));
        Assert.False(ScheduleEntityUpdater.HasValidMusicProperties(new ScheduleStateItem
        {
            MusicPublicationCode = "",
            MusicTrackCode = "t",
        }));
        Assert.True(ScheduleEntityUpdater.HasValidMusicProperties(new ScheduleStateItem
        {
            MusicPublicationCode = "pub",
            MusicTrackCode = "1",
        }));
    }

    [Fact]
    public void CreateMusicFromActionSchedule_maps_flat_state_to_entity()
    {
        var schedule = new ScheduleStateItem
        {
            MusicId = 44,
            MusicPublicationCode = "iam",
            MusicLanguageCode = null,
            MusicSectionCode = "s1",
            MusicTrackCode = "12",
            MusicRepeat = true,
        };

        var music = ScheduleEntityUpdater.CreateMusicFromActionSchedule(schedule, alarmScheduleId: 100);

        Assert.Equal(44, music.Id);
        Assert.Equal("iam", music.PublicationCode);
        Assert.Null(music.LanguageCode);
        Assert.Equal("s1", music.SectionCode);
        Assert.Equal("12", music.TrackCode);
        Assert.True(music.Repeat);
        Assert.Equal(100, music.AlarmScheduleId);
    }

    [Fact]
    public void UpdateBasicScheduleProperties_preserves_weekdays_when_db_schedule_has_none()
    {
        var existing = new AlarmSchedule { DaysOfWeek = WeekDays.Monday | WeekDays.Tuesday, Hour = 6, Minute = 10 };
        var dbSchedule = new AlarmSchedule { DaysOfWeek = 0, Hour = 8, Minute = 15 };

        ScheduleEntityUpdater.UpdateBasicScheduleProperties(existing, dbSchedule);

        Assert.Equal(WeekDays.Monday | WeekDays.Tuesday, existing.DaysOfWeek);
        Assert.Equal(8, existing.Hour);
        Assert.Equal(15, existing.Minute);
    }

    [Fact]
    public void UpdateScheduleEntity_clears_music_when_schedule_is_music_publication()
    {
        var existing = new AlarmSchedule
        {
            Id = 5,
            Music = new AlarmMusic
            {
                Id = 9,
                PublicationCode = "old",
                TrackCode = "1",
                AlarmScheduleId = 5,
            },
        };
        var dbSchedule = new AlarmSchedule { Id = 5 };
        var vm = new ScheduleStateItem
        {
            BiblePublicationIsMusic = true,
            BiblePublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
        };
        var action = new UpdateScheduleFromViewModelAction(vm);

        ScheduleEntityUpdater.UpdateScheduleEntity(existing, dbSchedule, action);

        Assert.Null(existing.Music);
    }

    [Fact]
    public void UpdateExistingBiblePublicationSchedule_resets_finished_duration_when_publication_updated()
    {
        var existing = new BiblePublicationSchedule
        {
            SectionCode = "40",
            TrackCode = "1",
            LanguageCode = "E",
            PublicationCode = "nwt",
            FinishedDuration = TimeSpan.FromMinutes(9),
        };
        var dbSchedule = new BiblePublicationSchedule
        {
            SectionCode = "41",
            TrackCode = "2",
            LanguageCode = "E",
            PublicationCode = "nwt",
            FinishedDuration = TimeSpan.FromMinutes(2),
        };

        ScheduleEntityUpdater.UpdateExistingBiblePublicationSchedule(
            existing,
            dbSchedule,
            new UpdateScheduleFromViewModelAction(new ScheduleStateItem(), biblePublicationUpdated: true));

        Assert.Equal("41", existing.SectionCode);
        Assert.Equal("2", existing.TrackCode);
        Assert.Equal(TimeSpan.Zero, existing.FinishedDuration);
    }
}
