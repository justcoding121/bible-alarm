#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class ReviewPromptDecisionEngineTests
{
    private static readonly DateTime AnchorUtc = new(2027, 1, 20, 15, 0, 0, DateTimeKind.Utc);

    private static ReviewPromptSnapshot EligibleBaseline(
        int? appOpensOverride = null,
        int? dismissOverride = null,
        DateTime? firstOpenUtc = null,
        DateTime? installUtc = null,
        DateTime? lastAttemptUtc = null,
        bool finalized = false) =>
        new(
            AppOpenCount: appOpensOverride ?? AppConstants.ReviewSettings.MinimumAppOpens,
            DismissCount: dismissOverride ?? AppConstants.ReviewSettings.MinimumDismissals,
            FirstOpenDateUtc: firstOpenUtc ?? AnchorUtc.AddDays(-AppConstants.ReviewSettings.MinimumDaysSinceFirstOpen),
            InstallDateUtc: installUtc ?? AnchorUtc.AddDays(-AppConstants.ReviewSettings.MinimumDaysSinceInstall),
            LastAttemptAtUtc: lastAttemptUtc,
            IsFinalized: finalized);

    [Fact]
    public void IsEligible_When_No_Criteria_Already_Finalized_ReturnsFalse()
    {
        var snapshot = EligibleBaseline(finalized: true);

        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_All_Minimums_And_Wait_Periods_Met_ReturnsTrue()
    {
        var snapshot = EligibleBaseline();

        Assert.True(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_AppOpens_Below_Minimum_ReturnsFalse()
    {
        var snapshot = EligibleBaseline(appOpensOverride: AppConstants.ReviewSettings.MinimumAppOpens - 1);

        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_Dismissals_Below_Minimum_ReturnsFalse()
    {
        var snapshot = EligibleBaseline(dismissOverride: AppConstants.ReviewSettings.MinimumDismissals - 1);

        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_FirstOpen_Date_Missing_Or_Too_Recent_ReturnsFalse()
    {
        var snapshotMissingFirst = EligibleBaseline() with { FirstOpenDateUtc = null };
        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshotMissingFirst, AnchorUtc));

        var snapshotTooSoon = EligibleBaseline(
            firstOpenUtc: AnchorUtc.AddDays(-(AppConstants.ReviewSettings.MinimumDaysSinceFirstOpen - 1)));
        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshotTooSoon, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_Install_Date_Missing_Or_Too_Recent_ReturnsFalse()
    {
        var snapshotMissingInstall = EligibleBaseline() with { InstallDateUtc = null };
        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshotMissingInstall, AnchorUtc));

        var snapshotTooSoon = EligibleBaseline(
            installUtc: AnchorUtc.AddDays(-(AppConstants.ReviewSettings.MinimumDaysSinceInstall - 1)));
        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshotTooSoon, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_Last_Attempt_Is_Inside_Retry_Cooldown_ReturnsFalse()
    {
        var lastAttemptTooRecent = AnchorUtc.AddDays(-(AppConstants.ReviewSettings.RetryAfterDaysWhenNotFinalized - 1));
        var snapshot = EligibleBaseline(lastAttemptUtc: lastAttemptTooRecent);

        Assert.False(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, AnchorUtc));
    }

    [Fact]
    public void IsEligible_When_Last_Attempt_IsPast_Retry_Window_ReturnsTrue()
    {
        var snapshot = EligibleBaseline(
            lastAttemptUtc: AnchorUtc.AddDays(-AppConstants.ReviewSettings.RetryAfterDaysWhenNotFinalized));

        Assert.True(ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, AnchorUtc));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void ShouldFinalizeFromLegacyReviewFlag_Truth_Table(
        bool legacyReviewRequestedExists,
        bool isAlreadyFinalized,
        bool expected)
    {
        Assert.Equal(
            expected,
            ReviewPromptDecisionEngine.ShouldFinalizeFromLegacyReviewFlag(
                legacyReviewRequestedExists,
                isAlreadyFinalized));
    }
}
