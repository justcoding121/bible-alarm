#nullable enable

using Bible;

namespace Bible.Alarm.Stores.Messages.ModalOverlay;

/// <summary>
/// Progress data for modal overlay (Publication/Section modals).
/// </summary>
public sealed class ModalOverlayFetchProgress
{
    /// <summary>
    /// Modal type: BiblePublication, BibleSection, MusicPublication, MusicSection.
    /// </summary>
    public string ModalType { get; init; } = string.Empty;

    public double Progress { get; init; }
    public string ProgressText { get; init; } = string.Empty;
    public bool IsVisible { get; init; }
}
