#nullable enable

using Bible.Alarm.Cataloger.Models.BiblePublications;
using Bible.Alarm.Cataloger.Utility;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class InMemoryDataStoreTests
{
    [Fact]
    public void BiblePublications_key_comparer_ignores_case_for_language_and_publication_code()
    {
        var sut = new InMemoryDataStore();
        var sections = new Dictionary<int, BiblePublicationSection>();
        var sectionCodeTrackMap = new Dictionary<int, Dictionary<int, BiblePublicationTrack>>();

        Assert.True(sut.BiblePublications.TryAdd(("E", "nwt"), ("NWT Study", sections, sectionCodeTrackMap)));
        Assert.False(sut.BiblePublications.TryAdd(("e", "NWT"), ("other", sections, sectionCodeTrackMap)));
        Assert.Single(sut.BiblePublications);
    }
}
