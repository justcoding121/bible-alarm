#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateItemTests
{
    [Fact]
    public void CompareTo_sorts_more_recent_LastPlayedAtUtc_before_older_when_ids_match()
    {
        var older = new ScheduleStateItem { Id = 1, LastPlayedAtUtc = new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var newer = new ScheduleStateItem { Id = 1, LastPlayedAtUtc = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc) };

        Assert.True(newer.CompareTo(older) < 0);
    }

    [Fact]
    public void CompareTo_breaks_LastPlayedAtUtc_ties_by_Id()
    {
        var t = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var low = new ScheduleStateItem { Id = 2, LastPlayedAtUtc = t };
        var high = new ScheduleStateItem { Id = 5, LastPlayedAtUtc = t };

        Assert.True(low.CompareTo(high) < 0);
    }

    [Fact]
    public void CompareTo_treats_null_other_as_less_than_any_item()
    {
        var sut = new ScheduleStateItem { Id = 1 };

        Assert.True(sut.CompareTo(null) > 0);
    }

    [Fact]
    public void CompareTo_object_delegates_to_schedule_state_overload()
    {
        var a = new ScheduleStateItem { Id = 1 };
        var b = new ScheduleStateItem { Id = 2 };

        Assert.Equal(a.CompareTo(b), a.CompareTo((object)b));
    }

    [Fact]
    public void Equals_requires_matching_Id_and_LastPlayedAtUtc()
    {
        var t = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = new ScheduleStateItem { Id = 7, LastPlayedAtUtc = t };
        var b = new ScheduleStateItem { Id = 7, LastPlayedAtUtc = t };
        var wrongTime = new ScheduleStateItem { Id = 7, LastPlayedAtUtc = t.AddDays(1) };

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(wrongTime));
    }

    [Fact]
    public void Equals_object_delegates_to_typed_equals()
    {
        var a = new ScheduleStateItem { Id = 3 };
        var b = new ScheduleStateItem { Id = 3 };

        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals(new object()));
    }

    [Fact]
    public void GetHashCode_matches_for_equal_schedules()
    {
        var t = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var x = new ScheduleStateItem { Id = 9, LastPlayedAtUtc = t };
        var y = new ScheduleStateItem { Id = 9, LastPlayedAtUtc = t };

        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void Equality_operators_use_value_semantics_not_reference_identity()
    {
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = new ScheduleStateItem { Id = 4, LastPlayedAtUtc = t };
        var b = new ScheduleStateItem { Id = 4, LastPlayedAtUtc = t };

        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Less_than_operator_reflects_CompareTo_ordering()
    {
        var older = new ScheduleStateItem { Id = 1, LastPlayedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var newer = new ScheduleStateItem { Id = 1, LastPlayedAtUtc = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        Assert.True(newer < older);
    }
}
