#nullable enable
using System.Threading;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// IFetchProgress implementation that sends modal overlay progress via WeakReferenceMessenger.
/// Use for Publication/Section modal open progress (spinner + progress bar).
/// </summary>
public sealed class ModalOverlayFetchProgressReporter : IFetchProgress
{
    private readonly string modalType;

    public CancellationToken CancellationToken { get; }

    public ModalOverlayFetchProgressReporter(string modalType, CancellationToken cancellationToken)
    {
        this.modalType = modalType;
        CancellationToken = cancellationToken;
    }

    public void UpdateProgress(double progress)
    {
        try
        {
            var clamped = Math.Max(0.0, Math.Min(1.0, progress));
            var percent = (int)Math.Round(clamped * 100);
            WeakReferenceMessenger.Default.Send(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = modalType,
                Progress = clamped,
                ProgressText = $"{percent}%",
                IsVisible = true
            }));
        }
        catch
        {
            // Progress messaging is non-critical when subscribers are torn down; ignore messenger failures.
        }
    }

    public void UpdateProgressText(string text)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = modalType,
                Progress = 0,
                ProgressText = text,
                IsVisible = true
            }));
        }
        catch
        {
            // Progress messaging is non-critical when subscribers are torn down; ignore messenger failures.
        }
    }

    public void SetIsVisible(bool isVisible)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = modalType,
                Progress = 0,
                ProgressText = isVisible ? "0%" : string.Empty,
                IsVisible = isVisible
            }));
        }
        catch
        {
            // Progress messaging is non-critical when subscribers are torn down; ignore messenger failures.
        }
    }
}
