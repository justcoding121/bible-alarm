#nullable enable

using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class TrackSelectionActionTests
{
    [Fact]
    public void Constructor_sets_CurrentBiblePublicationSchedule()
    {
        var bible = new BiblePublicationStateItem { Id = 8, PublicationCode = "nwt" };

        var sut = new TrackSelectionAction(bible);

        Assert.Same(bible, sut.CurrentBiblePublicationSchedule);
    }
}
