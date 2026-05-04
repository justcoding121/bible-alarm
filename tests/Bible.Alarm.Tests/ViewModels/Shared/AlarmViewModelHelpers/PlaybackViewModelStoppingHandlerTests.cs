#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelStoppingHandlerTests
{
    [Fact]
    public void ResetProgressUi_skips_when_user_is_interacting()
    {
        var applyCalls = 0;

        PlaybackViewModelStoppingHandler.ResetProgressUi(
            new SyncMainThreadScheduler(),
            () => true,
            () => { applyCalls++; });

        Assert.Equal(0, applyCalls);
    }

    [Fact]
    public void ResetProgressUi_applies_when_not_interacting_on_main_thread()
    {
        var applyCalls = 0;

        PlaybackViewModelStoppingHandler.ResetProgressUi(
            new SyncMainThreadScheduler(),
            () => false,
            () => applyCalls++);

        Assert.Equal(1, applyCalls);
    }

    [Fact]
    public void ResetProgressUi_applies_via_scheduler_when_not_on_main_thread()
    {
        var applyCalls = 0;

        PlaybackViewModelStoppingHandler.ResetProgressUi(
            new OffMainThreadSyncScheduler(),
            () => false,
            () => applyCalls++);

        Assert.Equal(1, applyCalls);
    }

    [Fact]
    public void BeginStoppingUi_applies_inline_when_on_main_thread()
    {
        var applyCalls = 0;

        PlaybackViewModelStoppingHandler.BeginStoppingUi(
            new SyncMainThreadScheduler(),
            () => applyCalls++);

        Assert.Equal(1, applyCalls);
    }

    [Fact]
    public void BeginStoppingUi_marshals_via_scheduler_when_not_on_main_thread()
    {
        var applyCalls = 0;

        PlaybackViewModelStoppingHandler.BeginStoppingUi(
            new OffMainThreadSyncScheduler(),
            () => applyCalls++);

        Assert.Equal(1, applyCalls);
    }
}
