#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class MauiNavigationUiThreadInvokerBibleAlarmTests(MauiUiFixture fixture)
{
    [Fact]
    public async Task InvokeOnUiThreadAsync_runs_work_when_maui_ready()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        _ = fixture;

        var sut = new MauiNavigationUiThreadInvoker();
        var ran = false;

        await sut.InvokeOnUiThreadAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.True(ran);
    }
}
