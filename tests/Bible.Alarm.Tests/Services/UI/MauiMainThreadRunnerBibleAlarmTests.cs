#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class MauiMainThreadRunnerBibleAlarmTests(MauiUiFixture fixture)
{
    [Fact]
    public async Task InvokeOnMainThreadAsync_executes_delegate()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadRunner();
        var invoked = false;

        await sut.InvokeOnMainThreadAsync(() =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        Assert.True(invoked);
    }

    [Fact]
    public void BeginInvokeOnMainThread_executes_action()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadRunner();
        var done = new ManualResetEventSlim(false);

        sut.BeginInvokeOnMainThread(() => done.Set());

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task InvokeOnMainThreadAsync_runs_work_that_mutates_captured_state()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadRunner();
        var value = 0;

        await sut.InvokeOnMainThreadAsync(() =>
        {
            value = 42;
            return Task.CompletedTask;
        });

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task InvokeOnMainThreadAsync_propagates_exceptions()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InvokeOnMainThreadAsync(() =>
                Task.FromException(new InvalidOperationException("main-thread-fail"))));
    }
}
