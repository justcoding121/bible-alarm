#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Plugin.StoreReview;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles review request logic for AlarmViewModal.
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
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened when review was requested.");
        }
    }

    private async Task ProcessDismissCount()
    {
        var dismissCount = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.DismissCount);

        if (dismissCount != null && dismissCount.Value != null && int.Parse(dismissCount.Value) >= 6)
        {
            await RequestReview();
        }
        else
        {
            await IncrementDismissCount(dismissCount);
        }
    }

    private async Task RequestReview()
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewRequested,
            "True");

        await MainThread.InvokeOnMainThreadAsync(async () =>
            await CrossStoreReview.Current.RequestReview(false));
    }

    private async Task IncrementDismissCount(Models.GeneralSettings? dismissCount)
    {
        if (dismissCount?.Value != null)
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.DismissCount,
                (int.Parse(dismissCount.Value) + 1).ToString());
        }
        else
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.DismissCount,
                "1");
        }
    }
}

