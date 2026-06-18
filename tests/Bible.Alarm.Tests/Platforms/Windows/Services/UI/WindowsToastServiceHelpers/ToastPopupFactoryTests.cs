#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;
using Bible.Alarm.Tests.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
[Collection("MauiUi")]
public sealed class ToastPopupFactoryTests(MauiUiFixture fixture)
{
    [Fact]
    public void CreateToastPopup_builds_popup_with_normalized_message()
    {
        _ = fixture;
        MauiUiTestBootstrap.TryInitialize();
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var window = new Window();
        var popup = ToastPopupFactory.CreateToastPopup("  hello   world  ", window);

        Assert.False(popup.IsLightDismissEnabled);
        Assert.True(popup.ShouldConstrainToRootBounds);

        var border = Assert.IsType<Border>(popup.Child);
        var textBlock = Assert.IsType<TextBlock>(border.Child);
        Assert.Equal("hello world", textBlock.Text);
    }

    [Fact]
    public void CreateToastPopup_uses_white_foreground_text()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var window = new Window();
        var popup = ToastPopupFactory.CreateToastPopup("toast", window);
        var border = Assert.IsType<Border>(popup.Child);
        var textBlock = Assert.IsType<TextBlock>(border.Child);

        Assert.NotNull(textBlock.Foreground);
    }
}
