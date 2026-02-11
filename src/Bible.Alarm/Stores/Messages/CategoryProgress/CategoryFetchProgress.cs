#nullable enable

namespace Bible.Alarm.Stores.Messages;

/// <summary>
/// Progress data for category fetch operation.
/// </summary>
public sealed class CategoryFetchProgress
{
    /// <summary>
    /// The category ID being fetched.
    /// </summary>
    public int CategoryId { get; init; }

    /// <summary>
    /// Progress value between 0.0 and 1.0.
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Whether the fetch has completed (success or failure).
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// True when fetch failed (e.g. network error); ViewModel should show toast and close modal.
    /// </summary>
    public bool HasError { get; init; }
}
