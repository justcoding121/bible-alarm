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
}
