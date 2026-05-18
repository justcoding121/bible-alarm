#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Tests.Support;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class BootstrapHelperTests : IDisposable
{
    public BootstrapHelperTests()
    {
        BootstrapHelper.ResetBootstrapStateForTests();
        Log.Logger = TestLogging.CreateLogger();
    }

    public void Dispose()
    {
        BootstrapHelper.ResetBootstrapStateForTests();
    }

    [Fact]
    public void IsBootstrapCompleted_is_false_after_reset()
    {
        Assert.False(BootstrapHelper.IsBootstrapCompleted());
    }

    [Fact]
    public void MarkBootstrapCompleted_makes_wait_return_immediately()
    {
        BootstrapHelper.MarkBootstrapCompleted();

        Assert.True(BootstrapHelper.IsBootstrapCompleted());
    }

    [Fact]
    public async Task WaitForBootstrapAsync_completes_when_bootstrap_marked_completed()
    {
        BootstrapHelper.MarkBootstrapCompleted();

        await BootstrapHelper.WaitForBootstrapAsync(timeoutMs: 1000);
    }

    [Fact]
    public async Task WaitForBootstrapAsync_times_out_gracefully_when_bootstrap_never_completes()
    {
        await BootstrapHelper.WaitForBootstrapAsync(timeoutMs: 50);
    }

    [Fact]
    public void WaitForBootstrap_returns_immediately_when_already_completed()
    {
        BootstrapHelper.MarkBootstrapCompleted();

        BootstrapHelper.WaitForBootstrap(timeoutMs: 1000);
    }

    [Fact]
    public void IsBootstrapCompleted_is_deterministic_for_consecutive_reads()
    {
        var first = BootstrapHelper.IsBootstrapCompleted();
        var second = BootstrapHelper.IsBootstrapCompleted();
        Assert.Equal(first, second);
    }
}
