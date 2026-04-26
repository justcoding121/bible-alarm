#nullable enable
using System;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Services.Schedule;

public sealed record ReviewPromptSnapshot(
    int AppOpenCount,
    int DismissCount,
    DateTime? FirstOpenDateUtc,
    DateTime? InstallDateUtc,
    DateTime? LastAttemptAtUtc,
    bool IsFinalized);

public static class ReviewPromptDecisionEngine
{
    public static bool IsEligibleForPrompt(ReviewPromptSnapshot snapshot, DateTime nowUtc)
    {
        if (snapshot.IsFinalized)
        {
            return false;
        }

        var meetsOpenCount = snapshot.AppOpenCount >= AppConstants.ReviewSettings.MinimumAppOpens;
        var meetsDismissCount = snapshot.DismissCount >= AppConstants.ReviewSettings.MinimumDismissals;
        var meetsFirstOpenDays = snapshot.FirstOpenDateUtc.HasValue &&
                                 (nowUtc - snapshot.FirstOpenDateUtc.Value).TotalDays >= AppConstants.ReviewSettings.MinimumDaysSinceFirstOpen;
        var meetsInstallDays = snapshot.InstallDateUtc.HasValue &&
                               (nowUtc - snapshot.InstallDateUtc.Value).TotalDays >= AppConstants.ReviewSettings.MinimumDaysSinceInstall;
        var meetsRetryWindow = !snapshot.LastAttemptAtUtc.HasValue ||
                               (nowUtc - snapshot.LastAttemptAtUtc.Value).TotalDays >= AppConstants.ReviewSettings.RetryAfterDaysWhenNotFinalized;

        return meetsOpenCount && meetsDismissCount && meetsFirstOpenDays && meetsInstallDays && meetsRetryWindow;
    }

    public static bool ShouldFinalizeFromLegacyReviewFlag(bool legacyReviewRequestedExists, bool isAlreadyFinalized) =>
        isAlreadyFinalized || legacyReviewRequestedExists;
}
