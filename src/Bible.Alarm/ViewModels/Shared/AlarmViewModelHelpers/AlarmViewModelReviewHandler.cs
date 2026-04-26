#nullable enable
using Bible.Alarm.Services.UI.Interfaces;
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
    private readonly IReviewPromptService reviewPromptService;

    public AlarmViewModelReviewHandler(ILogger logger, IReviewPromptService reviewPromptService)
    {
        this.logger = logger;
        this.reviewPromptService = reviewPromptService;
    }

    public async Task HandleReviewRequestAsync()
    {
        try
        {
            await reviewPromptService.RecordDismissEngagementAndRequestIfEligibleAsync();
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened when review was requested.");
        }
    }
}

