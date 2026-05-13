#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageLanguageCodeMaxLengthMetadataTests
{
    [Fact]
    public void LanguageCode_max_length_matches_EF_contract()
    {
        var max = typeof(Language).GetProperty(nameof(Language.LanguageCode))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(10, max.Length);
    }
}
