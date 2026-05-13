#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class DisposableOneShotGateTests
{
    [Fact]
    public void TryBegin_allows_first_transition_then_blocks_follow_up_calls()
    {
        var disposed = false;

        Assert.True(DisposableOneShotGate.TryBegin(ref disposed));
        Assert.True(disposed);
        Assert.False(DisposableOneShotGate.TryBegin(ref disposed));
    }
}
