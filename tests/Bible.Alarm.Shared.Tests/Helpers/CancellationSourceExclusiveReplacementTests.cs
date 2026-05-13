#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class CancellationSourceExclusiveReplacementTests
{
    [Fact]
    public void TryTakeExclusive_returns_false_when_slot_already_null()
    {
        CancellationTokenSource? slot = null;
        Assert.False(CancellationSourceExclusiveReplacement.TryTakeExclusive(ref slot, out var taken));
        Assert.Null(taken);
        Assert.Null(slot);
    }

    [Fact]
    public void TryTakeExclusive_nulls_slot_and_yields_prior_instance_for_teardown()
    {
        using var owned = new CancellationTokenSource();
        CancellationTokenSource? slot = owned;

        Assert.True(CancellationSourceExclusiveReplacement.TryTakeExclusive(ref slot, out var taken));
        Assert.Same(owned, taken);
        Assert.Null(slot);
    }

    [Fact]
    public void CancelDisposeSwallowDisposed_swallows_ObjectDisposedException_when_source_already_disposed()
    {
        var cts = new CancellationTokenSource();
        cts.Dispose();

        var ex = Record.Exception(() => CancellationSourceExclusiveReplacement.CancelDisposeSwallowDisposed(cts));
        Assert.Null(ex);
    }
}
