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
    public async Task ExecuteAsync_Generic_ReturnsNull_WhenTimedWaitFails_StringResult()
    {
        using var gate = new SemaphoreSlim(0);

        var value = await ConcurrencyHelper.ExecuteAsync(gate, async () =>
        {
            await Task.CompletedTask;
            return "no-timeout";
        }, timeoutMs: 40);

        Assert.Null(value);
    }

    [Fact]
    public async Task ExecuteAsync_ActionWithToken_Completes_And_ReleaseLock()
    {
        using var gate = new SemaphoreSlim(1);
        var ran = false;

        await ConcurrencyHelper.ExecuteAsync(gate, async () =>
        {
            ran = true;
            await Task.CompletedTask;
        }, cancellationToken: CancellationToken.None);

        Assert.True(ran);
        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task ExecuteAsync_GenericWithToken_ReturnsResult()
    {
        using var gate = new SemaphoreSlim(1);

        var v = await ConcurrencyHelper.ExecuteAsync(
            gate,
            async () =>
            {
                await Task.CompletedTask;
                return "ok";
            },
            cancellationToken: default);

        Assert.Equal("ok", v);
        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task ExecuteAsync_Action_ReportsDisposedReleaseViaCallbackWhenSemaphoreDisposedEarly()
    {
        var gate = new SemaphoreSlim(1);
        ObjectDisposedException? seen = null;

        await ConcurrencyHelper.ExecuteAsync(
            gate,
            async () =>
            {
                gate.Dispose();
                await Task.CompletedTask;
            },
            onDisposedException: ex => seen = ex);

        Assert.NotNull(seen);
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
