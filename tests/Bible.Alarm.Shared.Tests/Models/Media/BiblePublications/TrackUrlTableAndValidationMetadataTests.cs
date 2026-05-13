#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlTableAndValidationMetadataTests
{
    [Fact]
    public void Type_maps_to_TrackUrls_with_bounded_Url_length()
    {
        var table = typeof(TrackUrl).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("TrackUrls", table.Name);

        var urlLen = typeof(TrackUrl).GetProperty(nameof(TrackUrl.Url))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(2000, urlLen.Length);
    }
}
