#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackTrackCodeMaxLengthMetadataTests
{
    [Fact]
    public void TrackCode_max_length_matches_EF_contract()
    {
        var max = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.TrackCode))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(50, max.Length);
    }
}
