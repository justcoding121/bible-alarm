#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class SafeTeardownTests
{
    [Fact]
    public async Task CancelAsyncNoThrow_completes_when_source_active()
    {
        using var cts = new CancellationTokenSource();

        await SafeTeardown.CancelAsyncNoThrow(cts);

        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task CancelAsyncNoThrow_does_not_throw_when_already_disposed()
    {
        var cts = new CancellationTokenSource();
        cts.Dispose();

        await SafeTeardown.CancelAsyncNoThrow(cts);
    }

    [Fact]
    public void CancelDisposeNoThrow_null_is_no_op()
    {
        SafeTeardown.CancelDisposeNoThrow(null);
    }

    [Fact]
    public void CancelDisposeNoThrow_cancels_and_disposes()
    {
        var cts = new CancellationTokenSource();

        SafeTeardown.CancelDisposeNoThrow(cts);

        Assert.True(cts.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => cts.Cancel());
    }

    [Fact]
    public void CancelDisposeNoThrow_when_already_cancelled_still_disposes()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        SafeTeardown.CancelDisposeNoThrow(cts);

        Assert.Throws<ObjectDisposedException>(() => cts.Cancel());
    }

    [Fact]
    public void CancelDisposeNoThrow_twice_does_not_throw()
    {
        var cts = new CancellationTokenSource();

        SafeTeardown.CancelDisposeNoThrow(cts);
        SafeTeardown.CancelDisposeNoThrow(cts);
    }

    [Fact]
    public void CancelDisposeNoThrow_concurrent_dispose_does_not_throw()
    {
        var cts = new CancellationTokenSource();
        var barrier = new Barrier(2);

        var t1 = Task.Run(() =>
        {
            barrier.SignalAndWait();
            SafeTeardown.CancelDisposeNoThrow(cts);
        });
        var t2 = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        var ex = Record.Exception(() => Task.WaitAll(t1, t2));

        Assert.Null(ex);
    }

    [Fact]
    public void CancelDisposeNoThrow_when_already_disposed_does_not_throw()
    {
        var cts = new CancellationTokenSource();
        cts.Dispose();

        SafeTeardown.CancelDisposeNoThrow(cts);

        SafeTeardown.CancelDisposeNoThrow(cts);
    }

    [Fact]
    public void CancelDisposeNoThrow_when_already_cancelled_skips_cancel_and_disposes()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        SafeTeardown.CancelDisposeNoThrow(cts);

        Assert.Throws<ObjectDisposedException>(() => cts.Token);
    }

    [Fact]
    public void CancelDisposeNoThrow_when_cancel_throws_ObjectDisposedException_still_disposes()
    {
        var cts = new CancellationTokenSource();
        cts.Dispose();

        SafeTeardown.CancelDisposeNoThrow(cts);
    }

    [Fact]
    public void CancelDisposeNoThrow_when_cts_disposed_before_entry_still_swallows_on_Dispose()
    {
        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var cts = new CancellationTokenSource();

        var worker = Task.Run(() =>
        {
            entered.Set();
            release.Wait();
            SafeTeardown.CancelDisposeNoThrow(cts);
        });

        entered.Wait();
        cts.Dispose();
        release.Set();

        Assert.Null(Record.Exception(() => worker.Wait()));
    }

    [Fact]
    public void CancelDisposeNoThrow_swallows_ObjectDisposedException_when_dispose_throws()
    {
        var cts = new ThrowOnDisposeCancellationTokenSource();

        SafeTeardown.CancelDisposeNoThrow(cts);
    }

    [Fact]
    public void CancelDisposeNoThrow_parallel_invocations_do_not_throw()
    {
        var cts = new CancellationTokenSource();
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => SafeTeardown.CancelDisposeNoThrow(cts)))
            .ToArray();

        var ex = Record.Exception(() => Task.WaitAll(tasks));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CancelAsyncNoThrow_when_cancel_races_with_dispose_does_not_throw()
    {
        var cts = new CancellationTokenSource();
        var barrier = new Barrier(2);

        var cancelTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await SafeTeardown.CancelAsyncNoThrow(cts);
        });

        var disposeTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            cts.Dispose();
        });

        var ex = await Record.ExceptionAsync(async () => await Task.WhenAll(cancelTask, disposeTask));

        Assert.Null(ex);
    }

    private sealed class ThrowOnDisposeCancellationTokenSource : CancellationTokenSource
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new ObjectDisposedException(nameof(ThrowOnDisposeCancellationTokenSource));
            }

            base.Dispose(disposing);
        }
    }
}

