#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class ToastPositionManagerTests
{
    [Fact]
    public void SetInitialPopupPosition_does_not_throw_when_window_content_is_null_even_if_popup_argument_is_null()
    {
        var ex = Record.Exception(() => ToastPositionManager.SetInitialPopupPosition(popup: null!, windowContent: null));

        Assert.Null(ex);
    }

    [Fact]
    public void SubscribeToWindowSizeChanges_does_not_throw_when_window_content_is_null()
    {
        var ex = Record.Exception(() =>
            ToastPositionManager.SubscribeToWindowSizeChanges(windowContent: null, popup: null!, currentWindow: null!));

        Assert.Null(ex);
    }

    [Fact]
    public void UpdatePopupPosition_does_not_throw_when_window_and_popup_are_null()
    {
        var ex = Record.Exception(() => ToastPositionManager.UpdatePopupPosition(popup: null!, currentWindow: null));

        Assert.Null(ex);
    }
}
