#nullable enable

using Bible.Alarm.Shared.Services.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ReviewPromptSnapshotBibleAlarmTests
{
    [Fact]
    public void With_expression_updates_single_field_and_preserves_others()
    {
        var firstOpen = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var install = new DateTime(2024, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var lastAttempt = new DateTime(2024, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        var baseline = new ReviewPromptSnapshot(
            AppOpenCount: 5,
            DismissCount: 2,
            FirstOpenDateUtc: firstOpen,
            InstallDateUtc: install,
            LastAttemptAtUtc: lastAttempt,
            IsFinalized: false);

        var updated = baseline with { AppOpenCount = 6 };

        Assert.Equal(6, updated.AppOpenCount);
        Assert.Equal(2, updated.DismissCount);
        Assert.Equal(firstOpen, updated.FirstOpenDateUtc);
        Assert.Equal(install, updated.InstallDateUtc);
        Assert.Equal(lastAttempt, updated.LastAttemptAtUtc);
        Assert.False(updated.IsFinalized);
    }
}
