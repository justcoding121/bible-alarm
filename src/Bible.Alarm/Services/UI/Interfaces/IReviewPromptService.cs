namespace Bible.Alarm.Services.UI.Interfaces;

public interface IReviewPromptService
{
    Task RecordAppOpenAsync();
    Task RecordDismissEngagementAndRequestIfEligibleAsync();
}
