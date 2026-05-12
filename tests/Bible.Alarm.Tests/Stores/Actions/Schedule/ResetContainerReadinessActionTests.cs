#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ResetContainerReadinessActionTests
{
    [Fact]
    public void FluxorPayloadVersion_is_expected()
    {
        Assert.Equal((byte)1, ResetContainerReadinessAction.FluxorPayloadVersion);
    }
}
