#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguagePublicationCodeMaxLengthMetadataTests
{
    [Fact]
    public void PublicationCode_max_length_matches_EF_contract()
    {
        var max = typeof(PublicationLanguage).GetProperty(nameof(PublicationLanguage.PublicationCode))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(50, max.Length);
    }
}
