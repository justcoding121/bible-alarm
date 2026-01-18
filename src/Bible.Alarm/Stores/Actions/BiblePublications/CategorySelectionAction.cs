namespace Bible.Alarm.Stores.Actions.BiblePublications;

public class CategorySelectionAction(int categoryId, string categoryName)
{
    public int CategoryId { get; } = categoryId;
    public string CategoryName { get; } = categoryName;
}
