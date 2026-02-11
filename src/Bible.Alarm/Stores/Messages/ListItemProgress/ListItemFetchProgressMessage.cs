#nullable enable

using Bible;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Bible.Alarm.Stores.Messages.ListItemProgress;

/// <summary>
/// Message sent when a list item (language, publication, section) reports fetch progress.
/// ViewModels register and update the matching item's DownloadProgress.
/// </summary>
public sealed class ListItemFetchProgressMessage : ValueChangedMessage<ListItemFetchProgress>
{
    public ListItemFetchProgressMessage(ListItemFetchProgress value) : base(value)
    {
    }
}
