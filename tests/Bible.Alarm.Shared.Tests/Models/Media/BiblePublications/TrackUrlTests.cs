#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackUrlTests
{
    [Fact]
    public void Maps_to_TrackUrls_table_with_max_url_length()
    {
        var table = Assert.Single(typeof(TrackUrl).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>());
        Assert.Equal("TrackUrls", table.Name);

        var urlMax = Assert.Single(typeof(TrackUrl).GetProperty(nameof(TrackUrl.Url))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: true)
            .Cast<MaxLengthAttribute>());
        Assert.Equal(2000, urlMax.Length);
    }
}
