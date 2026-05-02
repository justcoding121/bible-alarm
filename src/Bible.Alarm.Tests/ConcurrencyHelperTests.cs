#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class ConcurrencyHelperTests
{
    [Fact]
    public async Task ExecuteAsync_Action_ReturnsFalse_WhenLockTimesOut()
    {
        using var gate = new SemaphoreSlim(0);

        var ran = await ConcurrencyHelper.ExecuteAsync(gate, async () =>
        {
            await Task.CompletedTask;
        }, timeoutMs: 30);

        Assert.False(ran);
    }

    [Fact]
    public async Task ExecuteAsync_Action_ReturnsTrue_WhenLockAcquired()
    {
        using var gate = new SemaphoreSlim(1);

        var ran = await ConcurrencyHelper.ExecuteAsync(gate, async () =>
        {
            await Task.Delay(10);
        }, timeoutMs: 5000);

        Assert.True(ran);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_ReturnsResult_WhenLockAcquired()
    {
        using var gate = new SemaphoreSlim(1);

        var value = await ConcurrencyHelper.ExecuteAsync(gate, async () =>
        {
            await Task.Delay(5);
            return 77;
        }, timeoutMs: 5000);

        Assert.Equal(77, value);
    }

    [Fact]
    public async Task ExecuteWithTimeoutAsync_ReturnsNull_WhenLockUnavailable()
    {
        using var gate = new SemaphoreSlim(0);

        var value = await ConcurrencyHelper.ExecuteWithTimeoutAsync(gate, async () =>
        {
            await Task.CompletedTask;
            return 3;
        }, timeoutMs: 25);

        Assert.Null(value);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationPropagates_ToInnerWork()
    {
        using var gate = new SemaphoreSlim(1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ConcurrencyHelper.ExecuteAsync(gate, async () =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
            }, cancellationToken: cts.Token));
    }
}
