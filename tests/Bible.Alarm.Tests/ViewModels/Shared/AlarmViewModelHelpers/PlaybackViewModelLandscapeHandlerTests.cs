#nullable enable

using System.Reflection;
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

    [Fact]
    public async Task RescheduleAutoHide_replaces_pending_timer_without_invoking_overlay()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;

        sut.ScheduleAutoHide(() => true, () => false, () => overlayInvocations++);
        sut.ScheduleAutoHide(() => true, () => false, () => overlayInvocations++);
        sut.CancelAutoHide();

        await Task.Delay(400);

        Assert.Equal(0, overlayInvocations);
    }

    [Fact]
    public async Task ScheduleAutoHide_after_delay_invokes_callback_when_still_valid()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;

        sut.ScheduleAutoHide(() => true, () => false, () => overlayInvocations++);

        await Task.Delay(3500);

        Assert.Equal(1, overlayInvocations);
    }

    [Fact]
    public async Task ScheduleAutoHide_when_shouldCancel_at_fire_time_does_not_invoke()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;
        var shouldCancel = false;

        sut.ScheduleAutoHide(() => true, () => shouldCancel, () => overlayInvocations++);

        await Task.Delay(500);
        shouldCancel = true;

        await Task.Delay(3200);

        Assert.Equal(0, overlayInvocations);
    }

    [Fact]
    public async Task ScheduleAutoHide_when_no_longer_landscape_at_fire_time_does_not_invoke()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;
        var isLandscape = true;

        sut.ScheduleAutoHide(() => isLandscape, () => false, () => overlayInvocations++);

        await Task.Delay(500);
        isLandscape = false;

        await Task.Delay(3200);

        Assert.Equal(0, overlayInvocations);
    }

    [Fact]
    public void CancelAutoHide_swallows_errors_when_cts_already_disposed()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var field = typeof(PlaybackViewModelLandscapeHandler).GetField(
            "autoHideCts",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var disposed = new CancellationTokenSource();
        disposed.Dispose();
        field.SetValue(sut, disposed);

        sut.CancelAutoHide();
    }

    [Fact]
    public void CancelAutoHide_swallows_errors_when_dispose_throws()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var field = typeof(PlaybackViewModelLandscapeHandler).GetField(
            "autoHideCts",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(sut, new ThrowOnDisposeCancellationTokenSource());

        sut.CancelAutoHide();
    }

    [Fact]
    public async Task ScheduleAutoHide_skips_overlay_when_should_cancel_on_main_thread()
    {
        var sut = new PlaybackViewModelLandscapeHandler(new SyncMainThreadScheduler());
        var overlayInvocations = 0;

        sut.ScheduleAutoHide(() => true, () => true, () => overlayInvocations++);

        await Task.Delay(3500);

        Assert.Equal(0, overlayInvocations);
    }

    private sealed class ThrowOnDisposeCancellationTokenSource : CancellationTokenSource
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new ObjectDisposedException(nameof(ThrowOnDisposeCancellationTokenSource));
            }

            base.Dispose(disposing);
        }
    }
}
