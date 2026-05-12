#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

/// <see cref="SchedulePropertySnapshot" /> lives on <see cref="HomeStateChangeHandler" />'s compilation unit.
public sealed class SchedulePropertySnapshotTests
{
    [Fact]
    public void SchedulePropertySnapshot_value_equality_matches_all_components()
    {
        var utc = new DateTime(2024, 3, 1, 14, 0, 0, DateTimeKind.Utc);

        var a = new SchedulePropertySnapshot("sec", "trk", "Alarm", 6, 15, WeekDays.Monday | WeekDays.Wednesday, utc);
        var b = new SchedulePropertySnapshot("sec", "trk", "Alarm", 6, 15, WeekDays.Monday | WeekDays.Wednesday, utc);

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(new SchedulePropertySnapshot("sec", "trk", "Alarm", 6, 15, WeekDays.Monday | WeekDays.Wednesday, utc.AddMinutes(1))));
    }

    [Fact]
    public void SchedulePropertySnapshot_deconstructs_projection_fields()
    {
        var utc = DateTime.UtcNow.Date;
        var snapshot = new SchedulePropertySnapshot(null, null, string.Empty, 0, 0, default, null);

        var (sectionCode, trackCode, name, hour, minute, days, played) = snapshot;

        Assert.Null(sectionCode);
        Assert.Null(trackCode);
        Assert.Equal(string.Empty, name);
        Assert.Equal(0, hour);
        Assert.Equal(0, minute);
        Assert.Equal(default(WeekDays), days);
        Assert.Null(played);

        snapshot = snapshot with { Name = "N", Hour = 1, Minute = 2, DaysOfWeek = WeekDays.All, LastPlayedAtUtc = utc };
        (_, _, name, hour, minute, days, played) = snapshot;

        Assert.Equal("N", name);
        Assert.Equal(1, hour);
        Assert.Equal(2, minute);
        Assert.Equal(WeekDays.All, days);
        Assert.Equal(utc, played);
    }
}
