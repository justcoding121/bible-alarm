#nullable enable
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Bible.Alarm.Stores.Messages;

/// <summary>
/// Message sent when modal open fetch reports progress (overlay spinner + progress bar).
/// ViewModels register and update ProgressPercent, ProgressText, ShowProgress.
/// </summary>
public sealed class ModalOverlayFetchProgressMessage : ValueChangedMessage<ModalOverlayFetchProgress>
{
    public ModalOverlayFetchProgressMessage(ModalOverlayFetchProgress value) : base(value)
    {
    }
}

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
