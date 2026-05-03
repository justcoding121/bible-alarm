#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguageModelTests
{
    [Fact]
    public void Default_ctor_uses_clr_defaults_for_keys_optional_language_and_publication_nav()
    {
        var sut = new SectionLanguage();

        Assert.Equal(0, sut.Id);
        Assert.Equal(string.Empty, sut.PublicationCode);
        Assert.Equal(string.Empty, sut.SectionCode);
        Assert.Null(sut.LanguageId);
        Assert.Null(sut.Language);
        Assert.Equal(0, sut.PublicationLanguageId);
        Assert.Null(sut.PublicationLanguage);

        sut.PublicationCode = "nwt";
        sut.SectionCode = "mk";
        sut.PublicationLanguageId = 99;
        sut.LanguageId = 3;

        Assert.Equal("nwt", sut.PublicationCode);
        Assert.Equal("mk", sut.SectionCode);
        Assert.Equal(99, sut.PublicationLanguageId);
        Assert.Equal(3, sut.LanguageId);
        Assert.Null(sut.Language);
        Assert.Null(sut.PublicationLanguage);
    }

    [Fact]
    public void Publication_and_section_codes_accept_exactly_max_annotation_length()
    {
        var fifty = new string('p', 50);
        var fiftySect = new string('s', 50);

        var sut = new SectionLanguage { PublicationCode = fifty, SectionCode = fiftySect };

        Assert.Equal(50, sut.PublicationCode.Length);
        Assert.Equal(50, sut.SectionCode.Length);
        Assert.All(sut.PublicationCode, c => Assert.Equal('p', c));
        Assert.All(sut.SectionCode, c => Assert.Equal('s', c));
    }

    [Fact]
    public void Publication_language_navigation_can_attach_after_primitive_fields()
    {
        var category = new Category { Id = 21, CategoryCode = "Mag" };
        var language = new Language
        {
            Id = 4,
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var pubLang = new PublicationLanguage
        {
            Id = 910,
            PublicationCode = "g",
            CategoryId = category.Id,
            Category = category,
            LanguageId = language.Id,
            Language = language,
        };

        var sut = new SectionLanguage
        {
            PublicationCode = pubLang.PublicationCode,
            SectionCode = "sec-attached-later",
            PublicationLanguageId = pubLang.Id,
        };

        Assert.Null(sut.PublicationLanguage);

        sut.PublicationLanguage = pubLang;
        Assert.Same(pubLang, sut.PublicationLanguage);
        Assert.Equal(pubLang.Id, sut.PublicationLanguageId);
    }

    [Fact]
    public void Optional_Language_navigation_can_attach_after_language_id_primitive()
    {
        var category = new Category { Id = 41, CategoryCode = "Vid" };
        var language = new Language
        {
            Id = 55,
            LanguageCode = "M",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var pubLang = new PublicationLanguage
        {
            Id = 700,
            PublicationCode = "sjjm",
            CategoryId = category.Id,
            Category = category,
            LanguageId = language.Id,
            Language = language,
        };

        var sut = new SectionLanguage
        {
            PublicationCode = "sjjm",
            SectionCode = "song-1",
            PublicationLanguageId = pubLang.Id,
            PublicationLanguage = pubLang,
            LanguageId = language.Id,
        };

        Assert.Null(sut.Language);

        sut.Language = language;
        Assert.Same(language, sut.Language);
        Assert.Equal(language.Id, sut.LanguageId);
    }

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
