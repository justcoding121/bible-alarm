#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackStringLengthMetadataTests
{
    [Fact]
    public void TrackCode_and_Title_use_expected_max_lengths()
    {
        Assert.Equal(50, MaxLengthOf(nameof(BiblePublicationTrack.TrackCode)));
        Assert.Equal(255, MaxLengthOf(nameof(BiblePublicationTrack.Title)));
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(BiblePublicationTrack).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;
}
