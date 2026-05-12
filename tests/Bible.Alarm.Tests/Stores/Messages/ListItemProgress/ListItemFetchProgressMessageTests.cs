#nullable enable

using Bible.Alarm.Stores.Messages.ListItemProgress;

namespace Bible.Alarm.Tests;

public sealed class ListItemFetchProgressMessageTests
{
    [Fact]
    public void Value_exposes_payload_from_constructor()
    {
        var payload = new ListItemFetchProgress
        {
            Context = "BibleLanguage",
            ItemId = "E",
            Progress = 0.25,
        };

        var sut = new ListItemFetchProgressMessage(payload);

        Assert.Same(payload, sut.Value);
    }
}
