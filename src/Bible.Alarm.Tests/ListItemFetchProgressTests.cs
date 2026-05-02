#nullable enable

using Bible.Alarm.Stores.Messages.ListItemProgress;

namespace Bible.Alarm.Tests;

public sealed class ListItemFetchProgressTests
{
    [Fact]
    public void Init_sets_progress_payload()
    {
        var sut = new ListItemFetchProgress
        {
            Context = "BiblePublication",
            ItemId = "pub-1",
            Progress = 0.42,
        };

        Assert.Equal("BiblePublication", sut.Context);
        Assert.Equal("pub-1", sut.ItemId);
        Assert.Equal(0.42, sut.Progress);
    }
}
