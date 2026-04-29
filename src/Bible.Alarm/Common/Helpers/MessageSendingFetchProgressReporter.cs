#nullable enable
using System.Threading;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// IFetchProgress implementation that sends progress via WeakReferenceMessenger.
/// Use for list item progress (language, publication, section rows).
/// </summary>
public sealed class ListItemFetchProgressReporter : IFetchProgress
{
    private readonly string context;
    private readonly string itemId;
    private readonly Action? onProgressReported;

    public CancellationToken CancellationToken { get; }

    public ListItemFetchProgressReporter(string context, string itemId, Action? onProgressReported = null, CancellationToken cancellationToken = default)
    {
        this.context = context;
        this.itemId = itemId;
        this.onProgressReported = onProgressReported;
        CancellationToken = cancellationToken;
    }

    public void UpdateProgress(double progress)
    {
        try
        {
            onProgressReported?.Invoke();
            var clamped = Math.Max(0.0, Math.Min(1.0, progress));
            WeakReferenceMessenger.Default.Send(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = context,
                ItemId = itemId,
                Progress = clamped
            }));
        }
        catch
        {
            // Ignore if receiver was disposed
        }
    }

    public void UpdateProgressText(string text)
    {
    }

    public void SetIsVisible(bool isVisible)
    {
    }
}
