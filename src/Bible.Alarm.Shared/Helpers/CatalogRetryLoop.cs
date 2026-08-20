#nullable enable

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Shared catalog-retry control flow: up to 10 attempts, 60s cap, increasing delays, stop on success/stagnation or a non-retryable network error.
/// </summary>
public static class CatalogRetryLoop
{
    public const int MaxRetries = 10;
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);
    public const int InitialDelayMs = 1000;
    public const int MaxDelayMs = 5000;

    public static int DelayMilliseconds(int attempt) =>
        Math.Min(InitialDelayMs * attempt, MaxDelayMs);

    public static async Task<CatalogRetryLoopResult<T>> RunAsync<T>(
        Func<CatalogRetryIterationContext, Task<CatalogRetryIterationResult<T>>> iterateAsync,
        Action<Exception, int>? onRetryableError = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var allCataloged = false;
        var attempt = 0;
        var previousCatalogedCount = -1;
        T? data = default;

        while (!allCataloged && attempt < MaxRetries && (DateTime.UtcNow - startTime) < MaxWait)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            try
            {
                var iteration = await iterateAsync(
                    new CatalogRetryIterationContext(attempt, previousCatalogedCount, cancellationToken));
                data = iteration.Snapshot;
                if (iteration.Completed)
                {
                    allCataloged = true;
                    continue;
                }

                if (iteration.BreakRetries)
                {
                    break;
                }

                previousCatalogedCount = iteration.UpdatedPreviousCatalogedCount;
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(ex))
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }

                onRetryableError?.Invoke(ex, attempt);
                await Task.Delay(DelayMilliseconds(attempt), cancellationToken);
            }
        }

        return new CatalogRetryLoopResult<T>(allCataloged, attempt, data);
    }
}

public readonly record struct CatalogRetryIterationContext(
    int Attempt,
    int PreviousCatalogedCount,
    CancellationToken CancellationToken);

public readonly record struct CatalogRetryIterationResult<T>(
    T? Snapshot,
    bool Completed,
    bool BreakRetries,
    int UpdatedPreviousCatalogedCount)
{
    public static CatalogRetryIterationResult<T> Success(T snapshot) =>
        new(snapshot, Completed: true, BreakRetries: false, UpdatedPreviousCatalogedCount: -1);

    public static CatalogRetryIterationResult<T> Stagnation(T? snapshot) =>
        new(snapshot, Completed: false, BreakRetries: true, UpdatedPreviousCatalogedCount: -1);

    public static CatalogRetryIterationResult<T> Continue(T? snapshot, int updatedPreviousCatalogedCount) =>
        new(snapshot, Completed: false, BreakRetries: false, UpdatedPreviousCatalogedCount: updatedPreviousCatalogedCount);
}

public readonly record struct CatalogRetryLoopResult<T>(
    bool AllCataloged,
    int Attempt,
    T? Data);
