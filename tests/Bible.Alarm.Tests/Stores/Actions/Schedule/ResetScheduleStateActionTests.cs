#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ResetScheduleStateActionTests
{
    [Fact]
    public void FluxorPayloadVersion_is_expected()
    {
        Assert.Equal((byte)1, ResetScheduleStateAction.FluxorPayloadVersion);
    }
}
