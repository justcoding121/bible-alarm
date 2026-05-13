#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageDirectionMaxLengthMetadataTests
{
    [Fact]
    public void Direction_max_length_matches_EF_contract()
    {
        var max = typeof(Language).GetProperty(nameof(Language.Direction))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(3, max.Length);
    }
}
