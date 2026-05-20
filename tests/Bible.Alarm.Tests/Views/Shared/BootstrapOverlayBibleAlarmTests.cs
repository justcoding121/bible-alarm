#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.Views.Shared;
using Microsoft.Maui.Graphics;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class BootstrapOverlayBibleAlarmTests(MauiUiFixture fixture)
{
    [Fact]
    public void Constructor_and_Deactivate_run_when_maui_ready()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        _ = fixture;

        var sut = new BootstrapOverlay();
        sut.Deactivate();

        Assert.Null(sut.Content);
        Assert.Equal(Colors.Transparent, sut.BackgroundColor);
    }
}
