#nullable enable

using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicCascadeScheduleMutationTests
{
    [Fact]
    public void Record_holds_values()
    {
        var sut = new MusicCascadeScheduleMutation(
            PublicationCode: "voc",
            PublicationName: "Vocal",
            SectionCode: "s1",
            SectionName: "Section A",
            TrackCode: "10",
            TrackTitle: "Song",
            PublicationModalItemCount: 5,
            SectionModalItemCount: 6);

        Assert.Equal("voc", sut.PublicationCode);
        Assert.Equal("Vocal", sut.PublicationName);
        Assert.Equal("s1", sut.SectionCode);
        Assert.Equal("Section A", sut.SectionName);
        Assert.Equal("10", sut.TrackCode);
        Assert.Equal("Song", sut.TrackTitle);
        Assert.Equal(5, sut.PublicationModalItemCount);
        Assert.Equal(6, sut.SectionModalItemCount);
    }
}
