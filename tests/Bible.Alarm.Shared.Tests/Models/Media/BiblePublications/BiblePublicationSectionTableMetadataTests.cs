#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionTableMetadataTests
{
    [Fact]
    public void Type_maps_to_BiblePublicationSections_table()
    {
        var table = typeof(BiblePublicationSection).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("BiblePublicationSections", table.Name);
    }
}
