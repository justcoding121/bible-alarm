#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class ToastLifecycleManagerTests
{
    [Fact]
    public async Task CloseExistingPopupIfNeededAsync_completes_when_no_popup_is_active()
    {
        var task = ToastLifecycleManager.CloseExistingPopupIfNeededAsync(null);

        await task;

        Assert.Equal(TaskStatus.RanToCompletion, task.Status);
    }

    [Fact]
    public void CleanupPopup_returns_when_popup_reference_is_null()
    {
        var ex = Record.Exception(() => ToastLifecycleManager.CleanupPopup(null));

        Assert.Null(ex);
    }
}
