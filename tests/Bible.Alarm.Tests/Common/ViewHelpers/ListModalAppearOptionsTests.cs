#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class ListModalAppearOptionsTests
{
    [Fact]
    public void Constructor_defaults_optional_callbacks_and_cts()
    {
        var sut = new ListModalAppearOptions(BusyOverlay: null, CollectionView: null);

        Assert.Null(sut.BusyOverlay);
        Assert.Null(sut.CollectionView);
        Assert.Null(sut.GetSelectedItem);
        Assert.Null(sut.RefreshAction);
        Assert.Null(sut.OnFetchFailed);
        Assert.Null(sut.GetItemCountFromViewModel);
        Assert.Equal(default, sut.CancellationToken);
    }
}
