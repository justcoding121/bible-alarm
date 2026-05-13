#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackTableMetadataTests
{
    [Fact]
    public void Type_maps_to_BiblePublicationTracks_table()
    {
        var table = typeof(BiblePublicationTrack).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("BiblePublicationTracks", table.Name);
    }
}
