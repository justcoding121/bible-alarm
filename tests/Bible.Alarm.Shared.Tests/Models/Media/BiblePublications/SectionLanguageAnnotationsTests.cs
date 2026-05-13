#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguageAnnotationsTests
{
    [Fact]
    public void Has_composite_unique_index_on_publication_section_and_language()
    {
        var idx = Assert.Single(
            typeof(SectionLanguage).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());

        Assert.True(idx.IsUnique);
        Assert.Equal(
            new[]
            {
                nameof(SectionLanguage.PublicationCode),
                nameof(SectionLanguage.SectionCode),
                nameof(SectionLanguage.LanguageId),
            },
            idx.PropertyNames);
    }
}
