#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class VocalMusicModelTests
{
    [Fact]
    public void Forwarding_members_delegate_to_underlying_BiblePublication()
    {
        var category = new Category { Id = 5, CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        var language = new Language
        {
            Id = 3,
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var publication = new BiblePublication
        {
            Id = 9,
            Name = "Songbook",
            PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
            LanguageId = language.Id,
            Language = language,
            Sections = [],
            Tracks = [],
            IsVideo = true,
            IsMusic = true,
        };

        publication.BiblePublicationCategories.Add(
            new BiblePublicationCategory
            {
                BiblePublication = publication,
                BiblePublicationId = publication.Id,
                Category = category,
                CategoryId = category.Id,
            });

        var sut = new VocalMusic { Publication = publication };

        Assert.Equal(publication.Id, sut.Id);
        Assert.Equal(publication.PublicationCode, sut.Code);
        Assert.Equal(publication.Name, sut.Name);
        Assert.Equal(publication.PrimaryCategoryId, sut.CategoryId);
        Assert.Same(category, sut.Category);
        Assert.Equal(language.Id, sut.LanguageId);
        Assert.Same(language, sut.Language);
        Assert.Same(publication.Sections, sut.Sections);
        Assert.Same(publication.Tracks, sut.Tracks);
        Assert.Equal(publication.IsVideo, sut.IsVideo);
    }

    [Fact]
    public void Implicit_casts_round_trip_between_VocalMusic_and_BiblePublication()
    {
        var publication = new BiblePublication
        {
            Id = 1,
            Name = "Music",
            PublicationCode = AppConstants.Media.MusicPublicationCodeSjjc,
            LanguageId = null,
            Language = null,
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = true,
        };

        VocalMusic wrapped = publication;
        Assert.Same(publication, wrapped.Publication);

        BiblePublication unwrapped = wrapped;
        Assert.Same(publication, unwrapped);
    }
}
