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

    [Fact]
    public void UpdateMusicFromDbSchedule_creates_music_entity_when_existing_alarm_had_none()
    {
        var existing = new AlarmSchedule { Id = 5, Music = null };
        var dbSchedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                Id = 9,
                PublicationCode = "iam",
                LanguageCode = null,
                SectionCode = "1",
                TrackCode = "2",
                Repeat = false,
                AlarmScheduleId = 5,
            },
        };

        ScheduleEntityUpdater.UpdateMusicFromDbSchedule(existing, dbSchedule);

        Assert.NotNull(existing.Music);
        Assert.Equal(9, existing.Music.Id);
        Assert.Equal("iam", existing.Music.PublicationCode);
        Assert.Equal(5, existing.Music.AlarmScheduleId);
    }

    [Fact]
    public void UpdateMusicFromDbSchedule_leaves_existing_unchanged_when_db_music_absent()
    {
        var existing = new AlarmSchedule { Id = 3, Music = null };
        var dbSchedule = new AlarmSchedule { Music = null };

        ScheduleEntityUpdater.UpdateMusicFromDbSchedule(existing, dbSchedule);

        Assert.Null(existing.Music);
    }

    [Fact]
    public void UpdateMusicFromActionSchedule_creates_music_when_db_entity_missing()
    {
        var existing = new AlarmSchedule { Id = 6, Music = null };
        var vm = new ScheduleStateItem
        {
            MusicPublicationCode = "iam",
            MusicTrackCode = "4",
            MusicLanguageCode = "E",
            MusicSectionCode = "1",
            MusicRepeat = true,
            MusicId = 11,
        };
        var action = new UpdateScheduleFromViewModelAction(vm, musicUpdated: true);

        ScheduleEntityUpdater.UpdateMusicFromActionSchedule(existing, action);

        Assert.NotNull(existing.Music);
        Assert.Equal("iam", existing.Music.PublicationCode);
        Assert.Equal("4", existing.Music.TrackCode);
        Assert.Equal(6, existing.Music.AlarmScheduleId);
    }

    [Fact]
    public void UpdateExistingMusicFromActionSchedule_updates_existing_track_codes()
    {
        var existing = new AlarmSchedule
        {
            Id = 8,
            Music = new AlarmMusic
            {
                Id = 3,
                PublicationCode = "old",
                TrackCode = "1",
                AlarmScheduleId = 8,
            },
        };
        var vm = new ScheduleStateItem
        {
            MusicPublicationCode = "iam",
            MusicTrackCode = "9",
            MusicLanguageCode = "MY",
            MusicSectionCode = "2",
            MusicRepeat = false,
            MusicId = 3,
        };

        ScheduleEntityUpdater.UpdateExistingMusicFromActionSchedule(existing, vm);

        Assert.Equal("iam", existing.Music!.PublicationCode);
        Assert.Equal("9", existing.Music.TrackCode);
        Assert.Equal("MY", existing.Music.LanguageCode);
    }

    [Fact]
    public void UpdateMusicFromDbSchedule_updates_existing_music_track()
    {
        var existing = new AlarmSchedule
        {
            Id = 4,
            Music = new AlarmMusic
            {
                Id = 2,
                PublicationCode = "old",
                TrackCode = "1",
                AlarmScheduleId = 4,
            },
        };
        var dbSchedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                Id = 2,
                PublicationCode = "iam",
                TrackCode = "7",
                LanguageCode = null,
                SectionCode = "1",
                Repeat = true,
                AlarmScheduleId = 4,
            },
        };

        ScheduleEntityUpdater.UpdateMusicFromDbSchedule(existing, dbSchedule);

        Assert.Equal("7", existing.Music!.TrackCode);
        Assert.Equal("iam", existing.Music.PublicationCode);
        Assert.True(existing.Music.Repeat);
    }

    [Fact]
    public void UpdateBiblePublicationEntity_attaches_bible_schedule_when_missing()
    {
        var existing = new AlarmSchedule { Id = 12, BiblePublicationSchedule = null };
        var dbSchedule = new AlarmSchedule
        {
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                LanguageCode = "E",
                SectionCode = "1",
                TrackCode = "1",
            },
        };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem());

        ScheduleEntityUpdater.UpdateBiblePublicationEntity(existing, dbSchedule, action);

        Assert.NotNull(existing.BiblePublicationSchedule);
        Assert.Equal(12, existing.BiblePublicationSchedule.AlarmScheduleId);
        Assert.Equal("nwt", existing.BiblePublicationSchedule.PublicationCode);
    }

    [Fact]
    public void UpdateMusicEntity_copies_from_db_when_db_music_present()
    {
        var existing = new AlarmSchedule
        {
            Id = 3,
            Music = new AlarmMusic { PublicationCode = "old", TrackCode = "1", AlarmScheduleId = 3 },
        };
        var dbSchedule = new AlarmSchedule
        {
            Music = new AlarmMusic
            {
                PublicationCode = "iam",
                TrackCode = "7",
                AlarmScheduleId = 3,
            },
        };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem());

        ScheduleEntityUpdater.UpdateMusicEntity(existing, dbSchedule, action);

        Assert.Equal("iam", existing.Music!.PublicationCode);
        Assert.Equal("7", existing.Music.TrackCode);
    }

    [Fact]
    public void UpdateMusicEntity_creates_music_from_action_when_db_music_null_and_valid_props()
    {
        var existing = new AlarmSchedule { Id = 4, Music = null };
        var dbSchedule = new AlarmSchedule { Music = null };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem
        {
            MusicPublicationCode = "osg",
            MusicTrackCode = "3",
        });

        ScheduleEntityUpdater.UpdateMusicEntity(existing, dbSchedule, action);

        Assert.NotNull(existing.Music);
        Assert.Equal("osg", existing.Music!.PublicationCode);
        Assert.Equal("3", existing.Music.TrackCode);
    }

    [Fact]
    public void UpdateMusicEntity_updates_existing_music_from_action_when_db_music_null()
    {
        var existing = new AlarmSchedule
        {
            Id = 5,
            Music = new AlarmMusic
            {
                PublicationCode = "old",
                TrackCode = "1",
                AlarmScheduleId = 5,
            },
        };
        var dbSchedule = new AlarmSchedule { Music = null };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem
        {
            MusicPublicationCode = "iam",
            MusicTrackCode = "9",
            MusicLanguageCode = "E",
        });

        ScheduleEntityUpdater.UpdateMusicEntity(existing, dbSchedule, action);

        Assert.Equal("iam", existing.Music!.PublicationCode);
        Assert.Equal("9", existing.Music.TrackCode);
        Assert.Equal("E", existing.Music.LanguageCode);
    }

    [Fact]
    public void UpdateMusicEntity_leaves_music_null_when_db_and_action_have_no_music()
    {
        var existing = new AlarmSchedule { Id = 6, Music = null };
        var dbSchedule = new AlarmSchedule { Music = null };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem());

        ScheduleEntityUpdater.UpdateMusicEntity(existing, dbSchedule, action);

        Assert.Null(existing.Music);
    }

    [Fact]
    public void UpdateBiblePublicationEntity_updates_existing_bible_schedule_from_db()
    {
        var existing = new AlarmSchedule
        {
            Id = 20,
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "old",
                LanguageCode = "E",
                SectionCode = "1",
                TrackCode = "1",
                AlarmScheduleId = 20,
            },
        };
        var dbSchedule = new AlarmSchedule
        {
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                LanguageCode = "E",
                SectionCode = "40",
                TrackCode = "2",
                AlarmScheduleId = 20,
            },
        };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem());

        ScheduleEntityUpdater.UpdateBiblePublicationEntity(existing, dbSchedule, action);

        Assert.Equal("40", existing.BiblePublicationSchedule!.SectionCode);
        Assert.Equal("2", existing.BiblePublicationSchedule.TrackCode);
        Assert.Equal("nwt", existing.BiblePublicationSchedule.PublicationCode);
    }

    [Fact]
    public void UpdateScheduleEntity_updates_music_when_music_updated_flag_true()
    {
        var existing = new AlarmSchedule
        {
            Id = 7,
            Music = new AlarmMusic
            {
                PublicationCode = "old",
                TrackCode = "1",
                AlarmScheduleId = 7,
            },
        };
        var dbSchedule = new AlarmSchedule
        {
            Id = 7,
            Music = new AlarmMusic
            {
                PublicationCode = "iam",
                TrackCode = "5",
                AlarmScheduleId = 7,
            },
        };
        var action = new UpdateScheduleFromViewModelAction(
            new ScheduleStateItem { BiblePublicationCode = "nwt" },
            musicUpdated: true);

        ScheduleEntityUpdater.UpdateScheduleEntity(existing, dbSchedule, action);

        Assert.Equal("iam", existing.Music!.PublicationCode);
        Assert.Equal("5", existing.Music.TrackCode);
    }

    [Fact]
    public void UpdateScheduleEntity_skips_music_update_when_music_not_flagged_updated()
    {
        var existing = new AlarmSchedule
        {
            Id = 2,
            Music = new AlarmMusic
            {
                PublicationCode = "keep",
                TrackCode = "1",
                AlarmScheduleId = 2,
            },
        };
        var dbSchedule = new AlarmSchedule
        {
            Id = 2,
            Music = new AlarmMusic
            {
                PublicationCode = "new",
                TrackCode = "9",
                AlarmScheduleId = 2,
            },
        };
        var action = new UpdateScheduleFromViewModelAction(new ScheduleStateItem(), musicUpdated: false);

        ScheduleEntityUpdater.UpdateScheduleEntity(existing, dbSchedule, action);

        Assert.Equal("keep", existing.Music!.PublicationCode);
    }
}
