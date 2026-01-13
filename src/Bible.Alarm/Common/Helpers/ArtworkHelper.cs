#nullable enable

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for artwork file operations with concurrency protection.
/// Provides a shared semaphore to prevent concurrent access to artwork directory operations.
/// </summary>
public static class ArtworkHelper
{
    // Static semaphore to prevent concurrent access to artwork directory operations
    // Multiple services may write artwork files concurrently, and they all write to the same artwork directory
    // This ensures directory creation, file enumeration, and file writes are serialized
    public static readonly SemaphoreSlim ArtworkLock = new(1, 1);
}
