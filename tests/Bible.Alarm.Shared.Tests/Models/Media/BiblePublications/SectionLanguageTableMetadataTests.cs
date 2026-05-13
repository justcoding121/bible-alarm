#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguageTableMetadataTests
{
    [Fact]
    public void Type_maps_to_SectionLanguages_table()
    {
        var table = typeof(SectionLanguage).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("SectionLanguages", table.Name);
    }
}
