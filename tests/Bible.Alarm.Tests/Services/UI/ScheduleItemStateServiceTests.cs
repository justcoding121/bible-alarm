#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleItemStateServiceTests
{
    [Fact]
    public void SetScheduleItemBusyToFalse_noops_for_invalid_ids()
    {
        var sut = new ScheduleItemStateService(TestLogging.CreateLogger());
        sut.SetScheduleItemBusyToFalse(null);
        sut.SetScheduleItemBusyToFalse(0);
        sut.SetScheduleItemBusyToFalse(-1);
    }

    [Fact]
    public void HideHomePageOverlay_does_not_throw_without_app_shell()
    {
        var sut = new ScheduleItemStateService(TestLogging.CreateLogger());
        sut.HideHomePageOverlay();
    }
}
