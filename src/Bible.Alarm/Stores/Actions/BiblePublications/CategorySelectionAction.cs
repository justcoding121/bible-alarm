#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Actions.BiblePublications;

public class CategorySelectionAction(int categoryId, string categoryName, string? previousLanguageCode = null, ScheduleStateItem? previousScheduleSnapshot = null)
{
    public int CategoryId { get; } = categoryId;
    public string CategoryName { get; } = categoryName;
    public string? PreviousLanguageCode { get; } = previousLanguageCode;

    /// <summary>
    /// Snapshot of the schedule before the category change. Used to revert state when auto-populate fails (e.g. network error).
    /// </summary>
    public ScheduleStateItem? PreviousScheduleSnapshot { get; } = previousScheduleSnapshot;
}
