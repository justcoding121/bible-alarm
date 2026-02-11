#nullable enable

using Bible;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Bible.Alarm.Stores.Messages.CategoryProgress;

/// <summary>
/// Message sent during category selection to report fetch progress.
/// Allows the CategorySelectionViewModel to show real progress on the list item.
/// </summary>
public sealed class CategoryFetchProgressMessage : ValueChangedMessage<CategoryFetchProgress>
{
    public CategoryFetchProgressMessage(CategoryFetchProgress value) : base(value)
    {
    }
}
