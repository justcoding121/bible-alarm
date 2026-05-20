#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class PublicationLanguageModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_can_be_read_and_written()
    {
        var category = new Category { Id = 3, CategoryCode = "Music" };
        var language = new Language { Id = 2, LanguageCode = "E" };
        var sectionLanguage = new SectionLanguage { Id = 4, PublicationCode = "nwt", SectionCode = "1" };

        var model = new PublicationLanguage
        {
            Id = 1,
            PublicationCode = "nwt",
            LanguageId = language.Id,
            Language = language,
            CatalogType = CatalogType.Sectioned,
            CategoryId = category.Id,
            Category = category,
            IsMusic = true,
            SectionLanguages = [sectionLanguage],
        };

        Assert.Equal(1, model.Id);
        Assert.Equal(CatalogType.Sectioned, model.CatalogType);
        Assert.Single(model.SectionLanguages);
        Assert.Same(sectionLanguage, model.SectionLanguages[0]);
    }
}
