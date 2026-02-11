#nullable enable

using Bible;

namespace Bible.Alarm.Stores.Messages.ListItemProgress;

/// <summary>
/// Progress data for list item fetch (language row, publication row, section row).
/// </summary>
public sealed class ListItemFetchProgress
{
    /// <summary>
    /// Context: BibleLanguage, BiblePublication, BibleSection, MusicLanguage, MusicPublication, MusicSection.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Item identifier (language code, publication code, section code).
    /// </summary>
    public string ItemId { get; init; } = string.Empty;

    /// <summary>
    /// Progress value between 0.0 and 1.0.
    /// </summary>
    public double Progress { get; init; }
}
