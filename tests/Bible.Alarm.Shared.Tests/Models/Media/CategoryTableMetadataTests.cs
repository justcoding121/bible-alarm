#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryTableMetadataTests
{
    [Fact]
    public void Type_maps_to_Categories_table()
    {
        var table = typeof(Category).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("Categories", table.Name);
    }
}
