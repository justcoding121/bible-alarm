#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguageModelTests
{
    [Fact]
    public void SectionLanguage_links_publication_language_and_optional_language_row()
    {
        var category = new Category { Id = 12, CategoryCode = "Bible" };
        var language = new Language
        {
            Id = 8,
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var pubLang = new PublicationLanguage
        {
            Id = 60,
            PublicationCode = "nwt",
            CategoryId = category.Id,
            Category = category,
            LanguageId = language.Id,
            Language = language,
        };

        var sut = new SectionLanguage
        {
            Id = 501,
            PublicationCode = "nwt",
            SectionCode = "mat-1",
            LanguageId = language.Id,
            Language = language,
            PublicationLanguageId = pubLang.Id,
            PublicationLanguage = pubLang,
        };

        Assert.Equal(501, sut.Id);
        Assert.Equal("nwt", sut.PublicationCode);
        Assert.Equal("mat-1", sut.SectionCode);
        Assert.Equal(8, sut.LanguageId);
        Assert.Same(language, sut.Language);
        Assert.Equal(60, sut.PublicationLanguageId);
        Assert.Same(pubLang, sut.PublicationLanguage);
    }

    [Fact]
    public void SectionLanguage_allows_null_language_for_instrumental_like_sections()
    {
        var category = new Category { Id = 3, CategoryCode = "Music" };
        var pubLang = new PublicationLanguage
        {
            Id = 7,
            PublicationCode = "iam",
            CategoryId = category.Id,
            Category = category,
            LanguageId = null,
            Language = null,
        };

        var sut = new SectionLanguage
        {
            Id = 2,
            PublicationCode = "iam",
            SectionCode = "disc-a",
            LanguageId = null,
            Language = null,
            PublicationLanguageId = pubLang.Id,
            PublicationLanguage = pubLang,
        };

        Assert.Null(sut.LanguageId);
        Assert.Null(sut.Language);
        Assert.Equal("disc-a", sut.SectionCode);
    }
}
