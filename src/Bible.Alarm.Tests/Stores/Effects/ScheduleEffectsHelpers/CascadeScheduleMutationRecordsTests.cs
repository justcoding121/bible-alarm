#nullable enable

using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;

namespace Bible.Alarm.Tests;

public sealed class CascadeScheduleMutationRecordsTests
{
    [Fact]
    public void MusicCascadeScheduleMutation_records_equal_when_components_match()
    {
        var a = new MusicCascadeScheduleMutation(
            PublicationCode: "iam",
            PublicationName: "Melodies",
            SectionCode: "iam-1",
            SectionName: "Disc",
            TrackCode: "2",
            TrackTitle: "Song",
            PublicationModalItemCount: 4,
            SectionModalItemCount: 8);

        var b = new MusicCascadeScheduleMutation(
            PublicationCode: "iam",
            PublicationName: "Melodies",
            SectionCode: "iam-1",
            SectionName: "Disc",
            TrackCode: "2",
            TrackTitle: "Song",
            PublicationModalItemCount: 4,
            SectionModalItemCount: 8);

        Assert.Equal(a, b);
    }

    [Fact]
    public void BiblePublicationCascadeScheduleMutation_carries_publication_without_language_flag()
    {
        var withLang = new BiblePublicationCascadeScheduleMutation(
            PublicationCode: "nwt",
            PublicationName: "NWT",
            SectionCode: "40",
            SectionName: "Matthew",
            TrackCode: "1",
            TrackTitle: "One",
            PublicationModalItemCount: 1,
            SectionModalItemCount: 2,
            TrackModalItemCount: 3,
            PublicationWithoutLanguage: false);

        var withoutLang = withLang with { PublicationWithoutLanguage = true };

        Assert.False(withLang.PublicationWithoutLanguage);
        Assert.True(withoutLang.PublicationWithoutLanguage);
        Assert.Equal(withLang.PublicationCode, withoutLang.PublicationCode);
    }
}
