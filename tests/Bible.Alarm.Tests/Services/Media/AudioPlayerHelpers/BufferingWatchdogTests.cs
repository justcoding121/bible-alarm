#nullable enable

using System.Reflection;
using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class BufferingWatchdogTests
{
    [Fact]
    public async Task OnBufferingStarted_fires_recovery_once_after_timeout()
    {
        var invocations = 0;
        using var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => invocations++, TimeSpan.FromMilliseconds(60));

        sut.OnBufferingStarted();

        await Task.Delay(200);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Second_OnBufferingStarted_while_watchdog_running_is_ignored()
    {
        var invocations = 0;
        using var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => invocations++, TimeSpan.FromMilliseconds(200));

        sut.OnBufferingStarted();
        sut.OnBufferingStarted();

        await Task.Delay(400);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Cancel_before_timeout_skips_recovery()
    {
        var invocations = 0;
        using var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => invocations++, TimeSpan.FromMilliseconds(200));

        sut.OnBufferingStarted();
        sut.Cancel();

        await Task.Delay(280);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task Dispose_before_timeout_skips_recovery()
    {
        var invocations = 0;
        var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => invocations++, TimeSpan.FromMilliseconds(200));

        sut.OnBufferingStarted();
        sut.Dispose();

        await Task.Delay(280);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void OnBufferingStarted_after_disposal_does_not_start_watchdog()
    {
        var invocations = 0;
        var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => invocations++, TimeSpan.FromMilliseconds(60));
        sut.Dispose();

        sut.OnBufferingStarted();

        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task Watchdog_can_run_again_after_stall_recovery_finishes()
    {
        var invocations = 0;
        using var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => invocations++, TimeSpan.FromMilliseconds(55));

        sut.OnBufferingStarted();
        await Task.Delay(150);

        sut.OnBufferingStarted();
        await Task.Delay(150);

        Assert.Equal(2, invocations);
    }

    [Fact]
    public void Cancel_swallows_ObjectDisposedException_when_internal_cts_was_pre_disposed()
    {
        var sut = new BufferingWatchdog(TestLogging.CreateLogger(), () => { }, TimeSpan.FromMilliseconds(60));
        var field = typeof(BufferingWatchdog).GetField("watchdogCts", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var leaked = new CancellationTokenSource();
        leaked.Dispose();
        field!.SetValue(sut, leaked);

        sut.Cancel();

        Assert.Null(field.GetValue(sut));
    }
}
