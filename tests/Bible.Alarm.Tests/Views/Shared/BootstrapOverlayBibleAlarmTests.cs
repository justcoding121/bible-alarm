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
        if (!MauiUiTestBootstrap.IsReady || Application.Current is null)
        {
            return;
        }

        _ = fixture;

        EnsureBootstrapOverlayThemeResources();

        var sut = new BootstrapOverlay();
        sut.Deactivate();

        Assert.Null(sut.Content);
        Assert.Equal(Colors.Transparent, sut.BackgroundColor);
    }

    private static void EnsureBootstrapOverlayThemeResources()
    {
        var resources = Application.Current!.Resources;
        if (!resources.ContainsKey("PrimaryColor"))
        {
            resources["PrimaryColor"] = Colors.Purple;
        }

        if (!resources.ContainsKey("PageBackgroundColor"))
        {
            resources["PageBackgroundColor"] = Colors.Transparent;
        }
    }
}
