#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class ToastLifecycleManagerTests
{
    [Fact]
    public async Task CloseExistingPopupIfNeededAsync_completes_when_popup_null()
    {
        await ToastLifecycleManager.CloseExistingPopupIfNeededAsync(null);
    }

    [Fact]
    public void CleanupPopup_returns_when_popup_null()
    {
        ToastLifecycleManager.CleanupPopup(null);
    }
}
