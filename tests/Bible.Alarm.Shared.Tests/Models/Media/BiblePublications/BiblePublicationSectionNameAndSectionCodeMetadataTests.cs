#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionNameAndSectionCodeMetadataTests
{
    [Fact]
    public void Name_and_section_code_max_lengths_match_EF_contract()
    {
        Assert.Equal(100, MaxLengthOf(nameof(BiblePublicationSection.Name)));
        Assert.Equal(50, MaxLengthOf(nameof(BiblePublicationSection.SectionCode)));
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(BiblePublicationSection).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;
}
