#nullable enable

using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationCascadeScheduleMutationTests
{
    [Fact]
    public void Record_holds_values_and_default_without_language_flag()
    {
        var sut = new BiblePublicationCascadeScheduleUpdater.Mutation(
            PublicationCode: "nwt",
            PublicationName: "NWT",
            SectionCode: "40",
            SectionName: "Matthew",
            TrackCode: "1",
            TrackTitle: "Mt 1",
            PublicationModalItemCount: 3,
            SectionModalItemCount: 2,
            TrackModalItemCount: 1,
            PublicationWithoutLanguage: true);

        Assert.Equal("nwt", sut.PublicationCode);
        Assert.Equal("NWT", sut.PublicationName);
        Assert.Equal("40", sut.SectionCode);
        Assert.Equal("Matthew", sut.SectionName);
        Assert.Equal("1", sut.TrackCode);
        Assert.Equal("Mt 1", sut.TrackTitle);
        Assert.Equal(3, sut.PublicationModalItemCount);
        Assert.Equal(2, sut.SectionModalItemCount);
        Assert.Equal(1, sut.TrackModalItemCount);
        Assert.True(sut.PublicationWithoutLanguage);
    }
}
