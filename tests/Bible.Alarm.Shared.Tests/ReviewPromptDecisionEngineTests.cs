using Bible.Alarm.Shared.Services.Schedule;

namespace Bible.Alarm.Shared.Tests;

public class ReviewPromptDecisionEngineTests
{
    [Fact]
    public void IsEligibleForPrompt_ReturnsTrue_WhenThresholdsAndRetryWindowAreMet()
    {
        var now = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ReviewPromptSnapshot(
            AppOpenCount: 7,
            DismissCount: 10,
            FirstOpenDateUtc: now.AddDays(-8),
            InstallDateUtc: now.AddDays(-10),
            LastAttemptAtUtc: now.AddDays(-31),
            IsFinalized: false);

        var isEligible = ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);

        Assert.True(isEligible);
    }

    [Fact]
    public void IsEligibleForPrompt_ReturnsFalse_WhenRetryWindowIsTooRecent()
    {
        var now = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ReviewPromptSnapshot(
            AppOpenCount: 12,
            DismissCount: 20,
            FirstOpenDateUtc: now.AddDays(-20),
            InstallDateUtc: now.AddDays(-20),
            LastAttemptAtUtc: now.AddDays(-10),
            IsFinalized: false);

        var isEligible = ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);

        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligibleForPrompt_ReturnsFalse_WhenAlreadyFinalized()
    {
        var now = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ReviewPromptSnapshot(
            AppOpenCount: 50,
            DismissCount: 50,
            FirstOpenDateUtc: now.AddDays(-50),
            InstallDateUtc: now.AddDays(-50),
            LastAttemptAtUtc: null,
            IsFinalized: true);

        var isEligible = ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);

        Assert.False(isEligible);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldFinalizeFromLegacyReviewFlag_RespectsLegacyAndExistingFinalization(
        bool legacyReviewRequestedExists,
        bool isAlreadyFinalized,
        bool expected)
    {
        var shouldFinalize = ReviewPromptDecisionEngine.ShouldFinalizeFromLegacyReviewFlag(
            legacyReviewRequestedExists,
            isAlreadyFinalized);

        Assert.Equal(expected, shouldFinalize);
    }
}
using Bible.Alarm.Shared.Services.Schedule;

namespace Bible.Alarm.Shared.Tests;

public class ReviewPromptDecisionEngineTests
{
    [Fact]
    public void IsEligibleForPrompt_ReturnsTrue_WhenThresholdsAndRetryWindowAreMet()
    {
        var now = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ReviewPromptSnapshot(
            AppOpenCount: 7,
            DismissCount: 10,
            FirstOpenDateUtc: now.AddDays(-8),
            InstallDateUtc: now.AddDays(-10),
            LastAttemptAtUtc: now.AddDays(-31),
            IsFinalized: false);

        var isEligible = ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);

        Assert.True(isEligible);
    }

    [Fact]
    public void IsEligibleForPrompt_ReturnsFalse_WhenRetryWindowIsTooRecent()
    {
        var now = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ReviewPromptSnapshot(
            AppOpenCount: 12,
            DismissCount: 20,
            FirstOpenDateUtc: now.AddDays(-20),
            InstallDateUtc: now.AddDays(-20),
            LastAttemptAtUtc: now.AddDays(-10),
            IsFinalized: false);

        var isEligible = ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);

        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligibleForPrompt_ReturnsFalse_WhenAlreadyFinalized()
    {
        var now = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ReviewPromptSnapshot(
            AppOpenCount: 50,
            DismissCount: 50,
            FirstOpenDateUtc: now.AddDays(-50),
            InstallDateUtc: now.AddDays(-50),
            LastAttemptAtUtc: null,
            IsFinalized: true);

        var isEligible = ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);

        Assert.False(isEligible);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldFinalizeFromLegacyReviewFlag_RespectsLegacyAndExistingFinalization(
        bool legacyReviewRequestedExists,
        bool isAlreadyFinalized,
        bool expected)
    {
        var shouldFinalize = ReviewPromptDecisionEngine.ShouldFinalizeFromLegacyReviewFlag(
            legacyReviewRequestedExists,
            isAlreadyFinalized);

        Assert.Equal(expected, shouldFinalize);
    }
}
