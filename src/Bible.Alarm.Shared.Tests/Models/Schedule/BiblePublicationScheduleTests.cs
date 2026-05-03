#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleTests
{
    private static AlarmSchedule MinimalAlarm()
    {
        return new AlarmSchedule
        {
            Id = 9001,
            Name = "Bp row",
            IsEnabled = true,
            Hour = 6,
            Minute = 5,
            Second = 4,
            DaysOfWeek = WeekDays.Monday | WeekDays.Friday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 7,
            NumberOfTracksToPlay = 2,
            AlwaysPlayFromStart = true,
            CurrentPlayItem = PlayType.Bible,
            LatestAlarmNotificationId = 0,
        };
    }

    [Fact]
    public void Default_ctor_uses_clr_defaults_for_strings_timespan_and_navigation()
    {
        var sut = new BiblePublicationSchedule();

        Assert.Equal(0, sut.Id);
        Assert.Null(sut.LanguageCode);
        Assert.Equal(string.Empty, sut.PublicationCode);
        Assert.Null(sut.SectionCode);
        Assert.Equal(string.Empty, sut.TrackCode);
        Assert.Equal(TimeSpan.Zero, sut.FinishedDuration);
        Assert.Equal(0, sut.AlarmScheduleId);
        Assert.Null(sut.AlarmSchedule);

        sut.PublicationCode = "bi12";
        sut.TrackCode = "72";
        sut.FinishedDuration = TimeSpan.FromTicks(777);
        sut.AlarmScheduleId = 901;

        Assert.Equal("bi12", sut.PublicationCode);
        Assert.Equal("72", sut.TrackCode);
        Assert.Equal(TimeSpan.FromTicks(777), sut.FinishedDuration);
        Assert.Equal(901, sut.AlarmScheduleId);
        Assert.Null(sut.AlarmSchedule);
    }

    [Fact]
    public void Code_fields_match_annotation_max_lengths()
    {
        var lang10 = new string('l', 10);
        var pub50 = new string('p', 50);
        var sect50 = new string('s', 50);
        var track50 = new string('t', 50);
        var alarm = MinimalAlarm();

        var sut = new BiblePublicationSchedule
        {
            LanguageCode = lang10,
            PublicationCode = pub50,
            SectionCode = sect50,
            TrackCode = track50,
            FinishedDuration = TimeSpan.Zero,
            AlarmScheduleId = alarm.Id,
            AlarmSchedule = alarm,
        };

        Assert.Equal(10, sut.LanguageCode!.Length);
        Assert.Equal(50, sut.PublicationCode.Length);
        Assert.Equal(50, sut.SectionCode!.Length);
        Assert.Equal(50, sut.TrackCode.Length);
    }

    [Fact]
    public void Alarm_schedule_navigation_can_attach_after_primitive_fields()
    {
        var alarm = MinimalAlarm();

        var sut = new BiblePublicationSchedule
        {
            PublicationCode = "vod",
            SectionCode = null,
            TrackCode = "3",
            FinishedDuration = TimeSpan.FromMilliseconds(440),
            AlarmScheduleId = alarm.Id,
        };

        Assert.Null(sut.AlarmSchedule);

        sut.AlarmSchedule = alarm;
        Assert.Same(alarm, sut.AlarmSchedule);
        Assert.Equal(alarm.Id, sut.AlarmScheduleId);
    }

    [Fact]
    public void Properties_roundtrip_with_alarm_navigation()
    {
        var alarm = MinimalAlarm();

        var sut = new BiblePublicationSchedule
        {
            Id = 44,
            LanguageCode = "E",
            PublicationCode = "vod",
            SectionCode = null,
            TrackCode = "12",
            FinishedDuration = TimeSpan.FromMinutes(4),
            AlarmScheduleId = alarm.Id,
            AlarmSchedule = alarm,
        };

        Assert.Equal(44, sut.Id);
        Assert.Equal("E", sut.LanguageCode);
        Assert.Null(sut.SectionCode);
        Assert.Equal("vod", sut.PublicationCode);
        Assert.Equal("12", sut.TrackCode);
        Assert.Equal(TimeSpan.FromMinutes(4), sut.FinishedDuration);
        Assert.Equal(9001, sut.AlarmScheduleId);
        Assert.Same(alarm, sut.AlarmSchedule);

        sut.SectionCode = "mat-8";
        Assert.Equal("mat-8", sut.SectionCode);
    }
}
