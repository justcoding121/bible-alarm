#nullable enable

using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests.Stores.Actions.BiblePublications;

public sealed class TrackSelectedActionTests
{
    [Fact]
    public void Constructor_sets_CurrentBiblePublicationSchedule()
    {
        var row = new BiblePublicationStateItem { Id = 2, PublicationCode = "nwt" };

        var sut = new TrackSelectedAction(row);

        Assert.Same(row, sut.CurrentBiblePublicationSchedule);
    }
}
