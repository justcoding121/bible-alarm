#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class SchedulePropertyCopierTests
{
    [Fact]
    public void CopyScheduleProperties_copies_all_reducer_managed_fields_onto_target()
    {
        var source = new ScheduleStateItem
        {
            Id = 42,
            Name = "Morning",
            IsEnabled = true,
            Hour = 7,
            Minute = 15,
            Second = 5,
            DaysOfWeek = WeekDays.Monday | WeekDays.Wednesday,
            NotificationEnabled = true,
            MusicEnabled = true,
            SnoozeMinutes = 9,
            NumberOfTracksToPlay = 3,
            AlwaysPlayFromStart = true,
            CurrentPlayItem = PlayType.Music,
            LatestAlarmNotificationId = 777,
            LastPlayedAtUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            BiblePublicationScheduleId = 10,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "40",
            BiblePublicationTrackCode = "1",
            BiblePublicationFinishedDuration = TimeSpan.FromMinutes(2),
            MusicId = 20,
            MusicPublicationCode = "pub",
            MusicLanguageCode = "E",
            MusicSectionCode = "sec",
            MusicTrackCode = "trk",
            MusicRepeat = true,
            BiblePublicationCategoryId = 100,
            BiblePublicationCategoryName = "Cat",
            BiblePublicationLanguageName = "English",
            BiblePublicationLanguageDirection = "ltr",
            BiblePublicationName = "NWT",
            BiblePublicationSectionName = "Matthew",
            BiblePublicationTrackTitle = "Title",
            MusicLanguageName = "En",
            MusicLanguageDirection = "ltr",
            MusicPublicationName = "P",
            MusicSectionName = "S",
            MusicTrackName = "T",
            BiblePublicationModalItemCount = 11,
            BiblePublicationSectionModalItemCount = 12,
            BiblePublicationTrackModalItemCount = 13,
            MusicPublicationModalItemCount = 14,
            MusicSectionModalItemCount = 15,
            BiblePublicationIsMusic = true,
        };

        var target = new ScheduleStateItem { Id = 0, Name = "old" };

        SchedulePropertyCopier.CopyScheduleProperties(target, source);

        Assert.Equal(42, target.Id);
        Assert.Equal("Morning", target.Name);
        Assert.True(target.IsEnabled);
        Assert.Equal(7, target.Hour);
        Assert.Equal(15, target.Minute);
        Assert.Equal(5, target.Second);
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday, target.DaysOfWeek);
        Assert.True(target.NotificationEnabled);
        Assert.True(target.MusicEnabled);
        Assert.Equal(9, target.SnoozeMinutes);
        Assert.Equal(3, target.NumberOfTracksToPlay);
        Assert.True(target.AlwaysPlayFromStart);
        Assert.Equal(PlayType.Music, target.CurrentPlayItem);
        Assert.Equal(777L, target.LatestAlarmNotificationId);
        Assert.Equal(10, target.BiblePublicationScheduleId);
        Assert.Equal("E", target.BiblePublicationLanguageCode);
        Assert.Equal("nwt", target.BiblePublicationCode);
        Assert.Equal("40", target.BiblePublicationSectionCode);
        Assert.Equal("1", target.BiblePublicationTrackCode);
        Assert.Equal(TimeSpan.FromMinutes(2), target.BiblePublicationFinishedDuration);
        Assert.Equal(20, target.MusicId);
        Assert.Equal("pub", target.MusicPublicationCode);
        Assert.Equal("E", target.MusicLanguageCode);
        Assert.Equal("sec", target.MusicSectionCode);
        Assert.Equal("trk", target.MusicTrackCode);
        Assert.True(target.MusicRepeat);
        Assert.Equal(100, target.BiblePublicationCategoryId);
        Assert.Equal("Cat", target.BiblePublicationCategoryName);
        Assert.Equal("English", target.BiblePublicationLanguageName);
        Assert.Equal("ltr", target.BiblePublicationLanguageDirection);
        Assert.Equal("NWT", target.BiblePublicationName);
        Assert.Equal("Matthew", target.BiblePublicationSectionName);
        Assert.Equal("Title", target.BiblePublicationTrackTitle);
        Assert.Equal("En", target.MusicLanguageName);
        Assert.Equal("ltr", target.MusicLanguageDirection);
        Assert.Equal("P", target.MusicPublicationName);
        Assert.Equal("S", target.MusicSectionName);
        Assert.Equal("T", target.MusicTrackName);
        Assert.Equal(11, target.BiblePublicationModalItemCount);
        Assert.Equal(12, target.BiblePublicationSectionModalItemCount);
        Assert.Equal(13, target.BiblePublicationTrackModalItemCount);
        Assert.Equal(14, target.MusicPublicationModalItemCount);
        Assert.Equal(15, target.MusicSectionModalItemCount);

        // Fields not managed by SchedulePropertyCopier stay at constructed defaults.
        Assert.Null(target.LastPlayedAtUtc);
        Assert.False(target.BiblePublicationIsMusic);
    }
}
