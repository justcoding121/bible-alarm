#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleTests
{
    private static AlarmSchedule Create(
        WeekDays days = WeekDays.Monday,
        int hour = 9,
        int minute = 30,
        int second = 0,
        int id = 10)
        => new()
        {
            Id = id,
            Name = "Test",
            IsEnabled = true,
            Hour = hour,
            Minute = minute,
            Second = second,
            DaysOfWeek = days,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 0,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
            LatestAlarmNotificationId = 0,
        };

    [Fact]
    public void Meridian_IsAm_WhenHourBeforeNoonElsePm()
    {
        Assert.Equal(Meridian.Am, Create(hour: 0).Meridian);
        Assert.Equal(Meridian.Am, Create(hour: 11).Meridian);
        Assert.Equal(Meridian.Pm, Create(hour: 12).Meridian);
        Assert.Equal(Meridian.Pm, Create(hour: 23).Meridian);
    }

    [Theory]
    [InlineData(0, 12)] // midnight displays as 12 in 12-hour form
    [InlineData(1, 1)]
    [InlineData(11, 11)]
    [InlineData(12, 12)] // noon
    [InlineData(13, 1)]
    [InlineData(23, 11)]
    public void MeridianHour_Maps24hToClockFace(int hour24, int expectedMeridianHour)
        => Assert.Equal(expectedMeridianHour, Create(hour: hour24).MeridianHour);

    [Fact]
    public void TimeText_UsesMeridianHourPadded_WithMinutePadded()
    {
        Assert.Equal("12:00", Create(hour: 0, minute: 0).TimeText);
        Assert.Equal("01:07", Create(hour: 13, minute: 7).TimeText);
    }

    [Fact]
    public void CronExpression_Encodes_Second_MinuteHour_AndSortedWeekdayList()
    {
        var sut = Create(WeekDays.Monday | WeekDays.Wednesday, hour: 14, minute: 5, second: 30);

        Assert.Equal("30 5 14 ? * 2,4", sut.CronExpression);
    }

    [Fact]
    public void NextFireDate_AfterAnchor_IsStrictlyLater()
    {
        var sut = Create(WeekDays.All, hour: 8, minute: 0);
        var after = new DateTimeOffset(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var next = sut.NextFireDate(after);

        Assert.True(next > after);
    }

    [Fact]
    public void NextFireDate_ThrowsInvalidOperation_WhenWeekdayMaskEmpty()
    {
        var sut = Create((WeekDays)0);

        Assert.Throws<InvalidOperationException>(() => sut.NextFireDate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NextFireDate_ThrowsInvalidOperation_WhenHourOutOfRange()
    {
        var sut = Create(WeekDays.Monday);
        sut.Hour = 24;

        Assert.Throws<InvalidOperationException>(() => sut.NextFireDate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CompareAndEquality_UseId_WhenNonZeroElseReference()
    {
        var a = Create(id: 1);
        var b = Create(id: 2);

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(a < b);
        Assert.False(a == b);

        var aRef = Create(id: 0);
        var bSameRef = aRef;

        Assert.True(aRef == bSameRef);

        var aDistinct = Create(id: 0);
        var bDistinct = Create(id: 0);
        Assert.False(aDistinct.Equals(bDistinct));
    }
}
