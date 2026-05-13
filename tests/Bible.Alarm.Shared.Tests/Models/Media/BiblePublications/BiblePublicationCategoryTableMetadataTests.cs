#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationCategoryTableMetadataTests
{
    [Fact]
    public void Type_maps_to_BiblePublicationCategories_table()
    {
        var table = typeof(BiblePublicationCategory).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("BiblePublicationCategories", table.Name);
    }
}
