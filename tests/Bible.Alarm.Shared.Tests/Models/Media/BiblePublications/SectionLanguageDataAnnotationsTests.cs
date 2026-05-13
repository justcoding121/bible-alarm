#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguageDataAnnotationsTests
{
    [Fact]
    public void Publication_and_section_codes_have_fifty_character_bound()
    {
        Assert.Equal(50, MaxLengthOf(nameof(SectionLanguage.PublicationCode)));
        Assert.Equal(50, MaxLengthOf(nameof(SectionLanguage.SectionCode)));
    }

    [Fact]
    public void PublicationLanguageId_foreign_key_targets_publication_language_navigation()
    {
        var fk = typeof(SectionLanguage).GetProperty(nameof(SectionLanguage.PublicationLanguageId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(SectionLanguage.PublicationLanguage), fk.Name);
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(SectionLanguage).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;
}
