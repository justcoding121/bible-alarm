#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ScheduledToastNotificationIdMatcherTests
{
    [Theory]
    [InlineData(7, "7", true)]
    [InlineData(7, "7_a", true)]
    [InlineData(7, "17", false)]
    [InlineData(7, "17_7", false)]
    [InlineData(12, "1_extra", false)]
    public void MatchesSchedule_accepts_legacy_id_or_prefix_form(int scheduleId, string toastId, bool expected) =>
        Assert.Equal(expected, ScheduledToastNotificationIdMatcher.MatchesSchedule(scheduleId, toastId));

    [Fact]
    public void MatchesSchedule_throws_when_toast_id_null()
    {
        Assert.Throws<ArgumentNullException>(() => ScheduledToastNotificationIdMatcher.MatchesSchedule(1, null!));
    }
}
