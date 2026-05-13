#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionSectionCodeMaxLengthMetadataTests
{
    [Fact]
    public void SectionCode_max_length_matches_EF_contract()
    {
        var max = typeof(BiblePublicationSection).GetProperty(nameof(BiblePublicationSection.SectionCode))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(50, max.Length);
    }
}
