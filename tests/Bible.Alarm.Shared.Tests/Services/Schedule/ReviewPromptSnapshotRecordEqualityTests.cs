#nullable enable

using Bible.Alarm.Shared.Services.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class ReviewPromptSnapshotRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_field_values_are_equal()
    {
        var a = new ReviewPromptSnapshot(
            AppOpenCount: 0,
            DismissCount: 0,
            FirstOpenDateUtc: null,
            InstallDateUtc: null,
            LastAttemptAtUtc: null,
            IsFinalized: false);

        var b = new ReviewPromptSnapshot(
            a.AppOpenCount,
            a.DismissCount,
            a.FirstOpenDateUtc,
            a.InstallDateUtc,
            a.LastAttemptAtUtc,
            a.IsFinalized);

        Assert.Equal(a, b);
    }
}
