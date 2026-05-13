#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationLanguageForeignKeyMetadataTests
{
    [Fact]
    public void LanguageId_foreign_key_targets_language_navigation()
    {
        var p = typeof(BiblePublication).GetProperty(
            nameof(BiblePublication.LanguageId),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotNull(p);
        var fk = p.GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(BiblePublication.Language), fk.Name);
    }
}
