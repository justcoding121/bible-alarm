#nullable enable

using Xunit;

namespace Bible.Alarm.Tests;

/// <summary>
/// Windows-only: on Android/iOS, notification toggles are gated by OS permission and this scenario
/// does not mirror headless Windows behaviour.
/// </summary>
[Trait("Platform", "Windows")]
public sealed partial class AlarmSettingsContainerViewModelTests
{
    [Fact]
    public void OnStateChanged_WhenSameSchedule_NotificationDiffers_SyncsFromStore()
    {
        var current = Schedule(5, isEnabled: true, notificationEnabled: false);
        var state = new MutableApplicationState(App(current));
        using var sut = CreateSut(state);

        state.Value.CurrentSchedule = Schedule(5, isEnabled: true, notificationEnabled: true);
        state.NotifyChanged();

        Assert.True(sut.NotificationEnabled);
    }
}
