#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageForeignKeyMetadataTests
{
    [Fact]
    public void Foreign_keys_target_language_and_category_navigations()
    {
        var fkLang = typeof(PublicationLanguage).GetProperty(nameof(PublicationLanguage.LanguageId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(PublicationLanguage.Language), fkLang.Name);

        var fkCat = typeof(PublicationLanguage).GetProperty(nameof(PublicationLanguage.CategoryId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(PublicationLanguage.Category), fkCat.Name);
    }
}
