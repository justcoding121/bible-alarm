#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class TranslatedPublicationLanguageForeignKeyMetadataTests
{
    [Fact]
    public void LanguageId_foreign_key_targets_language_navigation()
    {
        var fk = typeof(TranslatedPublication).GetProperty(nameof(TranslatedPublication.LanguageId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(TranslatedPublication.Language), fk.Name);
    }
}
