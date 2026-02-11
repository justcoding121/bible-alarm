#nullable enable

using Bible;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Bible.Alarm.Stores.Messages.ModalOverlay;

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
