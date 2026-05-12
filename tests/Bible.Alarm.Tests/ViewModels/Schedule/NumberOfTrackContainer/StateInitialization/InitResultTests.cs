#nullable enable

using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.StateInitialization;

namespace Bible.Alarm.Tests;

public sealed class InitResultTests
{
    [Fact]
    public void InitResult_holds_all_constructor_values()
    {
        var sut = new InitResult(42, NotificationEnabled: true, AlwaysPlayFromStart: false, PlayIndefinitely: true, LastCategoryName: "Bible");

        Assert.Equal(42, sut.ScheduleId);
        Assert.True(sut.NotificationEnabled);
        Assert.False(sut.AlwaysPlayFromStart);
        Assert.True(sut.PlayIndefinitely);
        Assert.Equal("Bible", sut.LastCategoryName);
    }

    [Fact]
    public void InitResult_two_instances_with_same_values_are_equal()
    {
        var a = new InitResult(7, true, false, false, null);
        var b = new InitResult(7, true, false, false, null);

        Assert.Equal(a, b);
    }

    [Fact]
    public void InitResult_with_expression_preserves_other_fields_when_one_changes()
    {
        var sut = new InitResult(1, false, false, false, "A");
        var copy = sut with { LastCategoryName = "Books" };

        Assert.Equal(sut.ScheduleId, copy.ScheduleId);
        Assert.False(copy.NotificationEnabled);
        Assert.Equal("Books", copy.LastCategoryName);
    }

    [Fact]
    public void InitResult_GetHashCode_matches_for_equal_instances()
    {
        var a = new InitResult(9, true, true, false, "");
        var b = new InitResult(9, true, true, false, "");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
