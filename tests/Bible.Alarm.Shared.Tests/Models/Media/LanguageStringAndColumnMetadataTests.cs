#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageStringAndColumnMetadataTests
{
    [Fact]
    public void LanguageCode_and_direction_max_lengths_and_column_mapping()
    {
        var codeProperty = typeof(Language).GetProperty(nameof(Language.LanguageCode))!;

        var codeLen = codeProperty.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(10, codeLen.Length);

        var column = codeProperty.GetCustomAttributes(typeof(ColumnAttribute), inherit: false)
            .Cast<ColumnAttribute>()
            .Single();
        Assert.Equal("LanguageCode", column.Name);

        var dirLen = typeof(Language).GetProperty(nameof(Language.Direction))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(3, dirLen.Length);
    }
}
