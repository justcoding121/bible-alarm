#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class VocalMusicTests
{
    [Fact]
    public void Implicit_conversion_from_publication_exposes_language_and_round_trips()
    {
        var language = new Language
        {
            LanguageCode = "FR",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var category = new Category { CategoryCode = "VocalCat" };
        var publication = new BiblePublication
        {
            Name = "Chants",
            PublicationCode = "voc-pub",
            LanguageId = 9,
            Language = language,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory
                {
                    BiblePublication = null!,
                    Category = category,
                    CategoryId = category.Id,
                },
            ],
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = true,
        };
        publication.BiblePublicationCategories[0].BiblePublication = publication;

        VocalMusic vocal = publication;

        Assert.Equal(9, vocal.LanguageId);
        Assert.Same(language, vocal.Language);
        Assert.Equal("voc-pub", vocal.Code);
        Assert.Same(category, vocal.Category);

        BiblePublication restored = vocal;
        Assert.Same(publication, restored);
    }
}
