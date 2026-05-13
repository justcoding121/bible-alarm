#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class MelodyMusicTests
{
    [Fact]
    public void Implicit_conversion_from_publication_exposes_projections_and_round_trips()
    {
        var category = new Category { CategoryCode = "MelodyCat" };
        var publication = new BiblePublication
        {
            Name = "Kingdom Melodies",
            PublicationCode = "iam",
            LanguageId = null,
            Language = null,
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

        MelodyMusic melody = publication;

        Assert.Equal(publication.Id, melody.Id);
        Assert.Equal("iam", melody.Code);
        Assert.Same(publication.PublicationCode, melody.Code);
        Assert.Same(category, melody.Category);
        Assert.Null(melody.Language);
        Assert.Same(publication.Sections, melody.Sections);
        Assert.Same(publication.Tracks, melody.Tracks);

        BiblePublication restored = melody;
        Assert.Same(publication, restored);
    }
}
