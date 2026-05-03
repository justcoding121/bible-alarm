#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelLandscapeHandlerTests
{
    [Fact]
    public void CancelAutoHide_is_safe_when_never_scheduled()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());

        sut.CancelAutoHide();
        sut.CancelAutoHide();
    }

    [Fact]
    public void ScheduleAutoHide_does_nothing_when_not_landscape()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;

        sut.ScheduleAutoHide(() => false, () => false, () => overlayInvocations++);

        Assert.Equal(0, overlayInvocations);
    }

    [Fact]
    public async Task ScheduleAutoHide_cancelled_before_delay_does_not_invoke_overlay()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;

        sut.ScheduleAutoHide(() => true, () => false, () => overlayInvocations++);
        sut.CancelAutoHide();

        await Task.Delay(400);

        Assert.Equal(0, overlayInvocations);
    }
}
