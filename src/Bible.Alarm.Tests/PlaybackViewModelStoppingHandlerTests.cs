#nullable enable

using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelStoppingHandlerTests
{
    [Fact]
    public void ResetProgressUi_skips_when_user_is_interacting()
    {
        var applyCalls = 0;

        PlaybackViewModelStoppingHandler.ResetProgressUi(() => true, () => applyCalls++);

        Assert.Equal(0, applyCalls);
    }
}
