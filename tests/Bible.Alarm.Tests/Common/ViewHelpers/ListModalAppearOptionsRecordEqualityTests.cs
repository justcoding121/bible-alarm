#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class ListModalAppearOptionsRecordEqualityTests
{
    [Fact]
    public void Instances_with_default_optionals_are_equal()
    {
        var a = new ListModalAppearOptions(BusyOverlay: null, CollectionView: null);
        var b = new ListModalAppearOptions(a.BusyOverlay, a.CollectionView);
        Assert.Equal(a, b);
    }
}
