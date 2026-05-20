#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class MauiMainThreadRunnerBibleAlarmTests(MauiUiFixture _)
{
    [Fact]
    public async Task InvokeOnMainThreadAsync_executes_delegate()
    {
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
}
