#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class MelodyMusicAndVocalMusicTests
{
    private static BiblePublication BuildPublication(int id, Language? lang, int? languageId, bool isVideo = false)
    {
        var category = new Category { Id = 90, CategoryCode = "Music" };
        var junction = new BiblePublicationCategory
        {
            BiblePublicationId = id,
            CategoryId = category.Id,
            Category = category,
        };
        var pub = new BiblePublication
        {
            Id = id,
            Name = "Kingdom Melodies",
            PublicationCode = "iam-release",
            Language = lang,
            LanguageId = languageId,
            Sections = [],
            Tracks = [],
            IsVideo = isVideo,
            IsMusic = true,
            BiblePublicationCategories = [junction],
        };
        junction.BiblePublication = pub;
        return pub;
    }

    [Fact]
    public void MelodyMusic_ImplicitCast_ProxiesPublicationMetadata_AndRoundTrips()
    {
        var pub = BuildPublication(id: 7, lang: null, languageId: null);

        MelodyMusic melody = pub;

        Assert.Equal(pub.Id, melody.Id);
        Assert.Equal(pub.PublicationCode, melody.Code);
        Assert.Equal(pub.Name, melody.Name);
        Assert.Null(melody.LanguageId);
        Assert.Null(melody.Language);
        Assert.Same(pub.Sections, melody.Sections);
        Assert.Same(pub.Tracks, melody.Tracks);
        Assert.False(melody.IsVideo);

        Assert.Same(pub, (BiblePublication)melody);
        Assert.Equal("Music", melody.Category.CategoryCode);
        Assert.Equal(90, melody.CategoryId);
    }

    [Fact]
    public void MelodyMusic_forwards_IsVideo_from_publication()
    {
        var pub = BuildPublication(id: 7, lang: null, languageId: null, isVideo: true);

        MelodyMusic melody = pub;

        Assert.True(melody.IsVideo);
    }

    [Fact]
    public void VocalMusic_ImplicitCast_Includes_Language_AndRoundTrips()
    {
        var lang = new Language
        {
            Id = 3,
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var pub = BuildPublication(id: 44, lang: lang, languageId: lang.Id);

        VocalMusic vocal = pub;

        Assert.Equal(lang.LanguageCode, vocal.Language!.LanguageCode);
        Assert.Equal(lang.Id, vocal.LanguageId);
        Assert.Equal(pub.PublicationCode, vocal.Code);
        Assert.Same(pub, (BiblePublication)vocal);
        Assert.Equal(90, vocal.CategoryId);
    }

    [Fact]
    public void VocalMusic_forwards_IsVideo_from_publication()
    {
        var lang = new Language
        {
            Id = 3,
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var pub = BuildPublication(id: 55, lang: lang, languageId: lang.Id, isVideo: true);

        VocalMusic vocal = pub;

        Assert.True(vocal.IsVideo);
    }
}
