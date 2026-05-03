#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageModelTests
{
    [Fact]
    public void Default_ctor_uses_clr_defaults_and_empty_section_list()
    {
        var sut = new PublicationLanguage();

        Assert.Equal(0, sut.Id);
        Assert.Equal(string.Empty, sut.PublicationCode);
        Assert.Null(sut.LanguageId);
        Assert.Null(sut.Language);
        Assert.Null(sut.CatalogType);
        Assert.Equal(0, sut.CategoryId);
        Assert.Null(sut.Category);
        Assert.False(sut.IsMusic);
        Assert.NotNull(sut.SectionLanguages);
        Assert.Empty(sut.SectionLanguages);
    }

    [Fact]
    public void PublicationCode_accepts_annotation_max_length()
    {
        var fifty = new string('p', 50);
        var sut = new PublicationLanguage { PublicationCode = fifty };

        Assert.Equal(50, sut.PublicationCode.Length);
    }

    [Fact]
    public void Category_navigation_attachable_before_setting_CategoryId()
    {
        var category = new Category { Id = 11, CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };

        var sut = new PublicationLanguage { PublicationCode = "sing", Category = category };

        sut.CategoryId = category.Id;

        Assert.Same(category, sut.Category);
        Assert.Equal(category.Id, sut.CategoryId);
    }

    [Fact]
    public void Instrumental_catalog_rows_allow_null_language_and_music_flag()
    {
        var category = new Category { Id = 3, CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };

        var sut = new PublicationLanguage
        {
            Id = 8,
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            LanguageId = null,
            Language = null,
            CategoryId = category.Id,
            Category = category,
            IsMusic = true,
            CatalogType = CatalogType.Sectioned,
        };

        Assert.Null(sut.Language);
        Assert.Null(sut.LanguageId);
        Assert.True(sut.IsMusic);
        Assert.Equal(CatalogType.Sectioned, sut.CatalogType);
    }

    [Fact]
    public void SectionLanguages_list_can_carry_junction_when_publication_language_row_stable()
    {
        var category = new Category { Id = 41, CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
        var language = new Language
        {
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var sut = new PublicationLanguage
        {
            Id = 600,
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            LanguageId = language.Id,
            Language = language,
            CategoryId = category.Id,
            Category = category,
            IsMusic = false,
            CatalogType = CatalogType.MediatorSectioned,
        };

        var junction = new SectionLanguage
        {
            Id = 1,
            PublicationCode = sut.PublicationCode,
            SectionCode = "mat-8",
            LanguageId = language.Id,
            Language = language,
            PublicationLanguageId = sut.Id,
            PublicationLanguage = sut,
        };
        sut.SectionLanguages.Add(junction);

        Assert.Single(sut.SectionLanguages);
        Assert.Same(junction, sut.SectionLanguages[0]);
        Assert.Equal(600, junction.PublicationLanguageId);
    }
}
