#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTableMetadataTests
{
    [Fact]
    public void Type_maps_to_BiblePublications_table()
    {
        var table = typeof(BiblePublication).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("BiblePublications", table.Name);
    }
}
