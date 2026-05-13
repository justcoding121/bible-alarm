#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationDataAnnotationsTests
{
    [Fact]
    public void Name_and_PublicationCode_use_expected_lengths_and_column_mapping()
    {
        var name = typeof(Publication).GetProperty(nameof(Publication.Name))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(255, name.Length);

        var code = typeof(Publication).GetProperty(nameof(Publication.PublicationCode))!;
        var codeLength = code.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(50, codeLength.Length);

        var column = code.GetCustomAttributes(typeof(ColumnAttribute), inherit: false)
            .Cast<ColumnAttribute>()
            .Single();
        Assert.Equal("PublicationCode", column.Name);
    }
}
