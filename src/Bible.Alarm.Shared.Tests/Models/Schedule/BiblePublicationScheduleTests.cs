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
