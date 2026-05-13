#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguageLanguageForeignKeyMetadataTests
{
    [Fact]
    public void LanguageId_foreign_key_targets_language_navigation()
    {
        var fk = typeof(SectionLanguage).GetProperty(nameof(SectionLanguage.LanguageId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(SectionLanguage.Language), fk.Name);
    }
}
