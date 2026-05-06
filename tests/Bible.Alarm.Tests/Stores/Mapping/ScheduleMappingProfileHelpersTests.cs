#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMappingProfileHelpersTests
{
    [Fact]
    public void AlarmScheduleToScheduleStateItemMap_returns_nulls_when_nested_entities_absent()
    {
        var src = new AlarmSchedule { Id = 1 };

        Assert.Null(AlarmScheduleToScheduleStateItemMap.BiblePublicationScheduleId(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.BiblePublicationLanguageCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.BiblePublicationCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.BiblePublicationSectionCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.BiblePublicationTrackCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.BiblePublicationFinishedDuration(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.MusicId(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.MusicPublicationCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.MusicLanguageCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.MusicSectionCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.MusicTrackCode(src));
        Assert.Null(AlarmScheduleToScheduleStateItemMap.MusicRepeat(src));
    }

    [Fact]
    public void AlarmScheduleToScheduleStateItemMap_projects_bible_and_music_when_present()
    {
        var bible = new BiblePublicationSchedule
        {
            Id = 9,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "  gen ",
            TrackCode = "1",
            FinishedDuration = TimeSpan.FromMinutes(3),
        };
        var music = new AlarmMusic
        {
            Id = 4,
            PublicationCode = "iam",
            LanguageCode = "E",
            SectionCode = "s1",
            TrackCode = "2",
            Repeat = true,
        };
        var src = new AlarmSchedule
        {
            Id = 10,
            BiblePublicationSchedule = bible,
            Music = music,
        };

        Assert.Equal(9, AlarmScheduleToScheduleStateItemMap.BiblePublicationScheduleId(src));
        Assert.Equal("E", AlarmScheduleToScheduleStateItemMap.BiblePublicationLanguageCode(src));
        Assert.Equal("nwt", AlarmScheduleToScheduleStateItemMap.BiblePublicationCode(src));
        Assert.NotNull(AlarmScheduleToScheduleStateItemMap.BiblePublicationSectionCode(src));
        Assert.Equal("1", AlarmScheduleToScheduleStateItemMap.BiblePublicationTrackCode(src));
        Assert.Equal(TimeSpan.FromMinutes(3), AlarmScheduleToScheduleStateItemMap.BiblePublicationFinishedDuration(src));
        Assert.Equal(4, AlarmScheduleToScheduleStateItemMap.MusicId(src));
        Assert.Equal("iam", AlarmScheduleToScheduleStateItemMap.MusicPublicationCode(src));
        Assert.Equal("E", AlarmScheduleToScheduleStateItemMap.MusicLanguageCode(src));
        Assert.Equal("s1", AlarmScheduleToScheduleStateItemMap.MusicSectionCode(src));
        Assert.Equal("2", AlarmScheduleToScheduleStateItemMap.MusicTrackCode(src));
        Assert.True(AlarmScheduleToScheduleStateItemMap.MusicRepeat(src));
    }

    [Fact]
    public void ScheduleStateItemToAlarmScheduleMap_BiblePublicationSchedule_null_without_link_id()
    {
        var row = new ScheduleStateItem { Id = 5, BiblePublicationScheduleId = null };

        Assert.Null(ScheduleStateItemToAlarmScheduleMap.BiblePublicationSchedule(row));
    }

    [Fact]
    public void ScheduleStateItemToAlarmScheduleMap_BiblePublicationSchedule_builds_when_id_set()
    {
        var row = new ScheduleStateItem
        {
            Id = 6,
            BiblePublicationScheduleId = 60,
            BiblePublicationLanguageCode = "MG",
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "exo",
            BiblePublicationTrackCode = "10",
            BiblePublicationFinishedDuration = TimeSpan.FromSeconds(90),
        };

        var child = ScheduleStateItemToAlarmScheduleMap.BiblePublicationSchedule(row);

        Assert.NotNull(child);
        Assert.Equal(60, child!.Id);
        Assert.Equal("MG", child.LanguageCode);
        Assert.Equal("nwt", child.PublicationCode);
        Assert.Equal(6, child.AlarmScheduleId);
        Assert.Equal(TimeSpan.FromSeconds(90), child.FinishedDuration);
        Assert.Equal("10", child.TrackCode);
        Assert.NotNull(child.SectionCode);
    }

    [Fact]
    public void ScheduleStateItemToAlarmScheduleMap_Music_null_when_no_music_signals()
    {
        var row = new ScheduleStateItem { Id = 1 };

        Assert.Null(ScheduleStateItemToAlarmScheduleMap.Music(row));
    }

    [Fact]
    public void ScheduleStateItemToAlarmScheduleMap_Music_from_publication_code_only()
    {
        var row = new ScheduleStateItem { Id = 2, MusicPublicationCode = "iam" };

        var m = ScheduleStateItemToAlarmScheduleMap.Music(row);

        Assert.NotNull(m);
        Assert.Equal(0, m!.Id);
        Assert.Equal("iam", m.PublicationCode);
        Assert.Equal(2, m.AlarmScheduleId);
        Assert.False(m.Repeat);
    }

    [Fact]
    public void ScheduleStateItemToAlarmScheduleMap_Music_from_music_id_only()
    {
        var row = new ScheduleStateItem { Id = 3, MusicId = 7 };

        var m = ScheduleStateItemToAlarmScheduleMap.Music(row);

        Assert.NotNull(m);
        Assert.Equal(7, m!.Id);
        Assert.Equal(string.Empty, m.PublicationCode);
        Assert.Equal(3, m.AlarmScheduleId);
    }
}
