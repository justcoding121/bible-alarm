#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.ApplicationModel;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class MauiMainThreadSchedulerBibleAlarmTests(MauiUiFixture _)
{

    [Fact]
    public void IsMainThread_reflects_maui_main_thread_state()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadScheduler();

        Assert.Equal(MainThread.IsMainThread, sut.IsMainThread);
    }

    [Fact]
    public async Task InvokeOnMainThreadAsync_runs_work_on_main_thread()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadScheduler();
        var invoked = false;

        await sut.InvokeOnMainThreadAsync(() =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        Assert.True(invoked);
    }

    [Fact]
    public void BeginInvokeOnMainThread_schedules_action()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var sut = new MauiMainThreadScheduler();
        var done = new ManualResetEventSlim(false);

        sut.BeginInvokeOnMainThread(() => done.Set());

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
    }
}
