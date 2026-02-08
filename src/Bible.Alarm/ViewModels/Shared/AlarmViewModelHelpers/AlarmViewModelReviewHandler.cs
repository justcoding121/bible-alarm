#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Plugin.StoreReview;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles review request logic for AlarmViewModal.
/// Implements industry best practices: time-based checks, engagement thresholds, and proper spacing.
/// Separated from AlarmViewModal for better modularity.
/// </summary>
public class AlarmViewModelReviewHandler
{
    private readonly ILogger logger;
    private readonly IGeneralSettingsService generalSettingsService;

    public AlarmViewModelReviewHandler(ILogger logger, IGeneralSettingsService generalSettingsService)
    {
        this.logger = logger;
        this.generalSettingsService = generalSettingsService;
    }

    public async Task HandleReviewRequestAsync()
    {
        try
        {
            await Task.Run(async () =>
            {
                if (!await generalSettingsService.GeneralSettingExistsAsync(
                        AppConstants.GeneralSettingsKeys.ReviewRequested))
                {
                    await ProcessDismissCount();
                }
                else
                {
                    logger.Debug("Review already requested - skipping review prompt");
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened when review was requested.");
        }
    }

    private async Task ProcessDismissCount()
    {
        var dismissCountSetting = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.DismissCount);
        var dismissCount = dismissCountSetting?.Value != null ? int.Parse(dismissCountSetting.Value) : 0;

        var firstDismissalDateSetting = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.FirstDismissalDate);
        var installDateSetting = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.AppInstallDate);

        var now = DateTime.UtcNow;
        var isFirstDismissal = dismissCount == 0;

        if (isFirstDismissal)
        {
            await RecordFirstDismissalAsync(now);
            logger.Debug("First dismissal recorded - count: 1, install date set");
            return;
        }

        var newDismissCount = dismissCount + 1;
        await IncrementDismissCountAsync(newDismissCount);

        if (newDismissCount < AppConstants.ReviewSettings.MinimumDismissals)
        {
            logger.Debug("Dismissal count {Count} below threshold {Threshold} - not requesting review yet",
                newDismissCount, AppConstants.ReviewSettings.MinimumDismissals);
            return;
        }

        var firstDismissalDate = ParseDate(firstDismissalDateSetting?.Value);
        var installDate = ParseDate(installDateSetting?.Value);

        if (!firstDismissalDate.HasValue)
        {
            logger.Warning("Dismissal count reached threshold but first dismissal date not found - recording now");
            await RecordFirstDismissalAsync(now);
            return;
        }

        if (!installDate.HasValue)
        {
            logger.Warning("Dismissal count reached threshold but install date not found - using first dismissal date");
            installDate = firstDismissalDate;
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.AppInstallDate,
                firstDismissalDate.Value.ToString("O"));
        }

        var daysSinceFirstDismissal = (now - firstDismissalDate.Value).TotalDays;
        var daysSinceInstall = (now - installDate.Value).TotalDays;

        var meetsDismissalThreshold = newDismissCount >= AppConstants.ReviewSettings.MinimumDismissals;
        var meetsTimeSinceDismissal = daysSinceFirstDismissal >= AppConstants.ReviewSettings.MinimumDaysSinceFirstDismissal;
        var meetsTimeSinceInstall = daysSinceInstall >= AppConstants.ReviewSettings.MinimumDaysSinceInstall;

        logger.Debug(
            "Review eligibility check - Dismissals: {Count}/{MinCount}, Days since first dismissal: {DaysSinceDismissal:.1f}/{MinDays}, Days since install: {DaysSinceInstall:.1f}/{MinDays}",
            newDismissCount, AppConstants.ReviewSettings.MinimumDismissals,
            daysSinceFirstDismissal, AppConstants.ReviewSettings.MinimumDaysSinceFirstDismissal,
            daysSinceInstall, AppConstants.ReviewSettings.MinimumDaysSinceInstall);

        if (meetsDismissalThreshold && meetsTimeSinceDismissal && meetsTimeSinceInstall)
        {
            logger.Information(
                "Review eligibility met - requesting review (dismissals: {Count}, days since first dismissal: {DaysSinceDismissal:.1f}, days since install: {DaysSinceInstall:.1f})",
                newDismissCount, daysSinceFirstDismissal, daysSinceInstall);
            await RequestReview();
        }
        else
        {
            logger.Debug("Review eligibility not met - waiting for more dismissals or time to pass");
        }
    }

    private async Task RecordFirstDismissalAsync(DateTime now)
    {
        var isoDate = now.ToString("O");

        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.FirstDismissalDate,
            isoDate);

        var installDateSetting = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.AppInstallDate);

        if (installDateSetting == null)
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.AppInstallDate,
                isoDate);
        }

        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.DismissCount,
            "1");
    }

    private async Task IncrementDismissCountAsync(int newCount)
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.DismissCount,
            newCount.ToString());
    }

    private async Task RequestReview()
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewRequested,
            "True");

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await CrossStoreReview.Current.RequestReview(false);
                logger.Information("Review request dialog displayed to user");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error displaying review request dialog");
            }
        });
    }

    private static DateTime? ParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return null;
        }

        if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
        {
            return date.ToUniversalTime();
        }

        return null;
    }
}

