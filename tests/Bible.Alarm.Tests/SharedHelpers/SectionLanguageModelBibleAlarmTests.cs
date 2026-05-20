#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class SectionLanguageModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_can_be_read_and_written()
    {
        var publicationLanguage = new PublicationLanguage
        {
            Id = 9,
            PublicationCode = "nwt",
            Category = new Category { Id = 1, CategoryCode = "Music" },
            CategoryId = 1,
        };

        var model = new SectionLanguage
        {
            Id = 5,
            PublicationCode = "nwt",
            SectionCode = "gen",
            LanguageId = 2,
            Language = new Language { Id = 2, LanguageCode = "E" },
            PublicationLanguageId = publicationLanguage.Id,
            PublicationLanguage = publicationLanguage,
        };

        Assert.Equal(5, model.Id);
        Assert.Equal(9, model.PublicationLanguageId);
        Assert.Same(publicationLanguage, model.PublicationLanguage);
    }
}
