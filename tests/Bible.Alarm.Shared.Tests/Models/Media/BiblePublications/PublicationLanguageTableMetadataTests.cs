#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageTableMetadataTests
{
    [Fact]
    public void Type_maps_to_PublicationLanguages_table()
    {
        var table = typeof(PublicationLanguage).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("PublicationLanguages", table.Name);
    }
}
