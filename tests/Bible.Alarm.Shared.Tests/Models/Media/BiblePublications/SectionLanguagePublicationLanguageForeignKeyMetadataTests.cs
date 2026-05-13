#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguagePublicationLanguageForeignKeyMetadataTests
{
    [Fact]
    public void PublicationLanguageId_foreign_key_targets_publication_language_navigation()
    {
        var fk = typeof(SectionLanguage).GetProperty(nameof(SectionLanguage.PublicationLanguageId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(SectionLanguage.PublicationLanguage), fk.Name);
    }
}
