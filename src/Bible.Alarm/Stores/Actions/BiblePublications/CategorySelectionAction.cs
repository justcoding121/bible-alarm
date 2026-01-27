namespace Bible.Alarm.Stores.Actions.BiblePublications;

public class CategorySelectionAction(int categoryId, string categoryName, string? previousLanguageCode = null)
{
    public int CategoryId { get; } = categoryId;
    public string CategoryName { get; } = categoryName;
    public string? PreviousLanguageCode { get; } = previousLanguageCode;
}
