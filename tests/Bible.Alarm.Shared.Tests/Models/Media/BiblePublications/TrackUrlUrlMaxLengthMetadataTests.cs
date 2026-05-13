#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlUrlMaxLengthMetadataTests
{
    [Fact]
    public void Url_max_length_matches_EF_contract()
    {
        var max = typeof(TrackUrl).GetProperty(nameof(TrackUrl.Url))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(2000, max.Length);
    }
}
