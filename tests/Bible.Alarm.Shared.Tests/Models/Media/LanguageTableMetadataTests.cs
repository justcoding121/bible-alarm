#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageTableMetadataTests
{
    [Fact]
    public void Type_maps_to_Languages_table()
    {
        var table = typeof(Language).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("Languages", table.Name);
    }
}
