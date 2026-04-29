#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Plugin.StoreReview;
using Serilog;
#if WINDOWS
using Microsoft.Maui;
#endif

namespace Bible.Alarm.Services.UI;

public sealed class ReviewPromptService(ILogger logger, IGeneralSettingsService generalSettingsService) : IReviewPromptService
{
    private readonly ILogger logger = logger;
    private readonly IGeneralSettingsService generalSettingsService = generalSettingsService;

    public async Task RecordAppOpenAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            await EnsureMigratedAsync(now);

            var lastCountedOpenAt = await GetDateAsync(AppConstants.GeneralSettingsKeys.ReviewLastCountedAppOpenAtUtc);
            if (lastCountedOpenAt.HasValue &&
                now - lastCountedOpenAt.Value < TimeSpan.FromMinutes(AppConstants.ReviewSettings.MinimumMinutesBetweenCountedAppOpens))
            {
                logger.Debug(
                    "Skipping app-open increment due to cooldown window ({CooldownMinutes}m)",
                    AppConstants.ReviewSettings.MinimumMinutesBetweenCountedAppOpens);
                return;
            }

            var appOpenCount = await GetIntAsync(AppConstants.GeneralSettingsKeys.ReviewAppOpenCount);
            appOpenCount++;

            await generalSettingsService.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewAppOpenCount, appOpenCount.ToString());
            await generalSettingsService.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewLastCountedAppOpenAtUtc, now.ToString("O"));

            var firstOpenDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.ReviewFirstOpenDate);
            if (!firstOpenDate.HasValue)
            {
                await generalSettingsService.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.ReviewFirstOpenDate, now.ToString("O"));
            }

            var installDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.AppInstallDate);
            if (!installDate.HasValue)
            {
                await generalSettingsService.SetGeneralSettingAsync(AppConstants.GeneralSettingsKeys.AppInstallDate, now.ToString("O"));
            }

            logger.Debug("Recorded app open count {AppOpenCount}", appOpenCount);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error recording app open for review prompting");
        }
    }

    public async Task RecordDismissEngagementAndRequestIfEligibleAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            await EnsureMigratedAsync(now);
            await IncrementDismissCountAsync(now);

            if (!await IsEligibleForPromptAsync(now))
            {
                return;
            }

            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.ReviewLastEligibleAtUtc,
                now.ToString("O"));

            await AttemptReviewRequestAsync(now);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error while processing review prompt flow after dismiss");
        }
    }

    private async Task IncrementDismissCountAsync(DateTime now)
    {
        var dismissCount = await GetIntAsync(AppConstants.GeneralSettingsKeys.DismissCount);
        dismissCount++;

        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.DismissCount,
            dismissCount.ToString());

        var firstDismissalDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.FirstDismissalDate);
        if (!firstDismissalDate.HasValue)
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.FirstDismissalDate,
                now.ToString("O"));
        }
    }

    private async Task<bool> IsEligibleForPromptAsync(DateTime now)
    {
        var appOpenCount = await GetIntAsync(AppConstants.GeneralSettingsKeys.ReviewAppOpenCount);
        var dismissCount = await GetIntAsync(AppConstants.GeneralSettingsKeys.DismissCount);
        var firstOpenDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.ReviewFirstOpenDate);
        var installDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.AppInstallDate);
        var lastAttemptAt = await GetDateAsync(AppConstants.GeneralSettingsKeys.ReviewLastAttemptAtUtc);
        var isFinalized = await GetBoolAsync(AppConstants.GeneralSettingsKeys.ReviewCompletedOrFinalized);

        var snapshot = new ReviewPromptSnapshot(
            appOpenCount,
            dismissCount,
            firstOpenDate,
            installDate,
            lastAttemptAt,
            isFinalized);

        logger.Debug(
            "Review eligibility inputs - opens:{OpenCount}/{MinOpen}, dismiss:{DismissCount}/{MinDismiss}, firstOpenDate:{FirstOpenDate}, installDate:{InstallDate}, lastAttemptAt:{LastAttemptAt}, finalized:{IsFinalized}",
            appOpenCount,
            AppConstants.ReviewSettings.MinimumAppOpens,
            dismissCount,
            AppConstants.ReviewSettings.MinimumDismissals,
            firstOpenDate,
            installDate,
            lastAttemptAt,
            isFinalized);

        return ReviewPromptDecisionEngine.IsEligibleForPrompt(snapshot, now);
    }

    private async Task AttemptReviewRequestAsync(DateTime now)
    {
        var attemptCount = await GetIntAsync(AppConstants.GeneralSettingsKeys.ReviewAttemptCount);
        attemptCount++;

        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewAttemptCount,
            attemptCount.ToString());
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewLastAttemptAtUtc,
            now.ToString("O"));

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                ConfigureWindowsStoreReviewWindow(logger);
                await CrossStoreReview.Current.RequestReview(false);
                logger.Information("Review request attempted successfully. Attempt #{AttemptCount}", attemptCount);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error requesting in-app review");
            }
        });
    }

    private async Task EnsureMigratedAsync(DateTime now)
    {
        if (await generalSettingsService.GeneralSettingExistsAsync(AppConstants.GeneralSettingsKeys.ReviewStateMigrated))
        {
            return;
        }

        var legacyReviewRequestedExists = await generalSettingsService.GeneralSettingExistsAsync(AppConstants.GeneralSettingsKeys.ReviewRequested);
        var alreadyFinalized = await GetBoolAsync(AppConstants.GeneralSettingsKeys.ReviewCompletedOrFinalized);
        if (ReviewPromptDecisionEngine.ShouldFinalizeFromLegacyReviewFlag(legacyReviewRequestedExists, alreadyFinalized))
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.ReviewCompletedOrFinalized,
                "True");
        }

        var installDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.AppInstallDate);
        if (!installDate.HasValue)
        {
            var firstDismissalDate = await GetDateAsync(AppConstants.GeneralSettingsKeys.FirstDismissalDate);
            if (firstDismissalDate.HasValue)
            {
                await generalSettingsService.SetGeneralSettingAsync(
                    AppConstants.GeneralSettingsKeys.AppInstallDate,
                    firstDismissalDate.Value.ToString("O"));
            }
            else
            {
                await generalSettingsService.SetGeneralSettingAsync(
                    AppConstants.GeneralSettingsKeys.AppInstallDate,
                    now.ToString("O"));
            }
        }

        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewStateMigrated,
            "True");
    }

    private async Task<int> GetIntAsync(string key)
    {
        var setting = await generalSettingsService.GetGeneralSettingAsync(key);
        if (setting?.Value == null)
        {
            return 0;
        }

        if (int.TryParse(setting.Value, out var parsed))
        {
            return parsed;
        }

        logger.Warning("Invalid integer value for review key {Key}: {Value}", key, setting.Value);
        return 0;
    }

    private async Task<DateTime?> GetDateAsync(string key)
    {
        var setting = await generalSettingsService.GetGeneralSettingAsync(key);
        if (string.IsNullOrWhiteSpace(setting?.Value))
        {
            return null;
        }

        if (DateTime.TryParse(
                setting.Value,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        logger.Warning("Invalid date value for review key {Key}: {Value}", key, setting.Value);
        return null;
    }

    private async Task<bool> GetBoolAsync(string key)
    {
        var setting = await generalSettingsService.GetGeneralSettingAsync(key);
        if (string.IsNullOrWhiteSpace(setting?.Value))
        {
            return false;
        }

        if (bool.TryParse(setting.Value, out var parsed))
        {
            return parsed;
        }

        logger.Warning("Invalid boolean value for review key {Key}: {Value}", key, setting.Value);
        return false;
    }

    private static void ConfigureWindowsStoreReviewWindow(ILogger logger)
    {
#if WINDOWS
        try
        {
            if (Application.Current?.Windows.Count > 0 &&
                Application.Current.Windows[0].Handler?.PlatformView is MauiWinUIWindow windowObject)
            {
                StoreReviewImplementation.Window = windowObject;
            }
            else
            {
                logger.Warning("Could not set review window handle on Windows because the MAUI window is unavailable.");
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to configure Windows review window handle");
        }
#endif
    }
}
