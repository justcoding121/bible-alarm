#nullable enable
using System.Threading;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Messages.CategoryProgress;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// IFetchProgress implementation that sends CategoryFetchProgressMessage.
/// Use when Effect/Handler needs to report progress for category list item.
/// </summary>
public sealed class CategoryFetchProgressReporter : IFetchProgress
{
    private readonly int categoryId;

    public CancellationToken CancellationToken { get; }

    public CategoryFetchProgressReporter(int categoryId, CancellationToken cancellationToken = default)
    {
        this.categoryId = categoryId;
        CancellationToken = cancellationToken;
    }

    public void UpdateProgress(double progress)
    {
        try
        {
            var clamped = Math.Max(0.0, Math.Min(1.0, progress));
            WeakReferenceMessenger.Default.Send(new CategoryFetchProgressMessage(new CategoryFetchProgress
            {
                CategoryId = categoryId,
                Progress = clamped,
                IsComplete = false,
                HasError = false
            }));
        }
        catch
        {
        }
    }

    public void UpdateProgressText(string text)
    {
    }

    public void SetIsVisible(bool isVisible)
    {
    }
}
