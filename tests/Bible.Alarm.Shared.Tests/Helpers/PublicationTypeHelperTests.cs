using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationTypeHelperTests
{
    [Fact]
    public void HasSectionStructure_True_ForEmptyOrWhitespacePublicationCode()
    {
        Assert.True(PublicationTypeHelper.HasSectionStructure(null));
        Assert.True(PublicationTypeHelper.HasSectionStructure(""));
        Assert.True(PublicationTypeHelper.HasSectionStructure(" "));
    }

    [Fact]
    public void HasSectionStructure_True_ForMagazinesAndIam()
    {
        Assert.True(PublicationTypeHelper.HasSectionStructure($"w{MagazineHelper.MagazineStartYear}"));
        Assert.True(PublicationTypeHelper.HasSectionStructure(AppConstants.Media.MelodyMusicPublicationCodeIam));
    }

    [Fact]
    public void HasSectionStructure_False_ForVocalMusicAndMediatorFlat()
    {
        Assert.False(PublicationTypeHelper.HasSectionStructure(AppConstants.Media.MusicPublicationCodeOsg));
        Assert.False(PublicationTypeHelper.HasSectionStructure(AppConstants.Media.MediatorPublicationCodeVODBibleTeachings));
    }

    [Fact]
    public void HasSectionStructure_True_DefaultFallsThroughToBibleShape()
    {
        Assert.True(PublicationTypeHelper.HasSectionStructure(AppConstants.Media.BiblePublicationCodeNwt));
        Assert.True(PublicationTypeHelper.HasSectionStructure("ZZZUnknownButNotMediator"));
    }

    [Fact]
    public void IsDrama_ReturnsFalse_ForNullOrUnknown()
    {
        Assert.False(PublicationTypeHelper.IsDrama(null));
        Assert.False(PublicationTypeHelper.IsDrama(""));
        Assert.False(PublicationTypeHelper.IsDrama("nwt"));
    }

    [Fact]
    public void IsDrama_ReturnsTrue_ForMp3CategoryAndDramaticReadings()
    {
        Assert.True(PublicationTypeHelper.IsDrama(AppConstants.Media.BiblePublicationCategoryDramas));
        Assert.True(PublicationTypeHelper.IsDrama(AppConstants.Media.BiblePublicationCodeDramaticBibleReadings));
    }

    [Theory]
    [InlineData("dramas", AppConstants.Media.BiblePublicationCategoryDramas)]
    [InlineData("DRAMAS", AppConstants.Media.BiblePublicationCategoryDramas)]
    [InlineData("dramaticbiblereadings", AppConstants.Media.BiblePublicationCodeDramaticBibleReadings)]
    public void GetCanonicalPublicationCodeForDatabase_NormalizesDiscoveredDramaCodes(
        string fromDiscovery,
        string expected)
    {
        Assert.Equal(expected, PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(fromDiscovery));
    }

    [Fact]
    public void GetCanonicalPublicationCodeForDatabase_PassesThroughWhenNotDrama()
    {
        const string code = "CustomPub";
        Assert.Equal(code, PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(code));
    }

    [Fact]
    public void IsVideo_False_ForNullMagazineAndAudioDramas()
    {
        Assert.False(PublicationTypeHelper.IsVideo(null));
        Assert.False(PublicationTypeHelper.IsVideo($"w{MagazineHelper.MagazineStartYear}"));
        Assert.False(PublicationTypeHelper.IsVideo(AppConstants.Media.BiblePublicationCategoryDramas));
        Assert.False(PublicationTypeHelper.IsVideo(AppConstants.Media.BiblePublicationCodeDramaticBibleReadings));
    }

    [Fact]
    public void IsVideo_True_ForKnownVideoPublicationsAndMediatorWhenNotAudioDrama()
    {
        Assert.True(PublicationTypeHelper.IsVideo(AppConstants.Media.BiblePublicationCodeDramasGoodNews));
        Assert.True(PublicationTypeHelper.IsVideo(AppConstants.Media.SeriesPublicationCodeThv));
        Assert.True(PublicationTypeHelper.IsVideo(AppConstants.Media.MediatorPublicationCodeVODBibleTeachings));
    }

    [Fact]
    public void GetTrackLabel_SwitchesBetweenTrackAndPart()
    {
        Assert.Equal(
            AppConstants.Media.PublicationUiTrackSingular,
            PublicationTypeHelper.GetTrackLabel(AppConstants.Media.BiblePublicationCodeNwt));
        Assert.Equal(
            AppConstants.Media.PublicationUiPartSingular,
            PublicationTypeHelper.GetTrackLabel(AppConstants.Media.MusicPublicationCodeOsg));
    }

    [Fact]
    public void GetTrackLabel_returns_track_label_when_publication_code_missing_matches_section_default()
    {
        Assert.Equal(
            AppConstants.Media.PublicationUiTrackSingular,
            PublicationTypeHelper.GetTrackLabel(null));
        Assert.Equal(
            AppConstants.Media.PublicationUiTrackSingular,
            PublicationTypeHelper.GetTrackLabel(""));
    }

    [Fact]
    public void GetCatalogType_Defaults_ToSectioned_WhenPublicationUnknown()
    {
        Assert.Equal(CatalogType.Sectioned, PublicationTypeHelper.GetCatalogType(null));
        Assert.Equal(CatalogType.Sectioned, PublicationTypeHelper.GetCatalogType(""));
    }

    [Fact]
    public void GetCatalogType_Magazines_AreIssueSectioned()
    {
        Assert.Equal(
            CatalogType.IssueSectioned,
            PublicationTypeHelper.GetCatalogType($"w{MagazineHelper.MagazineStartYear + 5}"));
    }

    [Fact]
    public void GetCatalogType_BibleEdition_IsSectioned()
    {
        Assert.Equal(CatalogType.Sectioned, PublicationTypeHelper.GetCatalogType(AppConstants.Media.BiblePublicationCodeNwt));
    }

    [Fact]
    public void GetCatalogType_MediatorPublication_IsMediatorSectioned()
    {
        Assert.Equal(
            CatalogType.MediatorSectioned,
            PublicationTypeHelper.GetCatalogType(AppConstants.Media.MediatorPublicationCodeStudioTalks));
    }

    [Fact]
    public void GetCatalogType_VocalMusic_IsFlat()
    {
        Assert.Equal(CatalogType.Flat, PublicationTypeHelper.GetCatalogType(AppConstants.Media.MusicPublicationCodeOsg));
    }

    [Fact]
    public void GetCatalogType_IamSectioned_WhenHasSectionStructure()
    {
        Assert.Equal(
            CatalogType.Sectioned,
            PublicationTypeHelper.GetCatalogType(AppConstants.Media.MelodyMusicPublicationCodeIam));
    }

    [Fact]
    public void HasSectionStructure_False_ForFlatBooksYearbooksBrochuresAndArticleSeries()
    {
        Assert.False(PublicationTypeHelper.HasSectionStructure("wcg"));
        Assert.False(PublicationTypeHelper.HasSectionStructure("yb12"));
        Assert.False(PublicationTypeHelper.HasSectionStructure("lmd"));
        Assert.False(PublicationTypeHelper.HasSectionStructure("mrt"));
    }

    [Fact]
    public void IsDrama_True_ForVodCodesRegisteredBesideTraditionalDramas()
    {
        Assert.True(PublicationTypeHelper.IsDrama(AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes));
        Assert.True(PublicationTypeHelper.IsDrama(AppConstants.Media.BiblePublicationCodeVODMoviesModernDay));
        Assert.True(PublicationTypeHelper.IsDrama(AppConstants.Media.BiblePublicationCodeVODMoviesExtras));
    }

    [Fact]
    public void GetCanonicalPublicationCodeForDatabase_NormalizesListedVodDiscoveryCodes_ToDramaticReadingsSlug()
    {
        Assert.Equal(
            AppConstants.Media.BiblePublicationCodeDramaticBibleReadings,
            PublicationTypeHelper.GetCanonicalPublicationCodeForDatabase(AppConstants.Media.BiblePublicationCodeVODMoviesAnimated));
    }

    [Fact]
    public void IsVideo_True_ForVodMoviesListedAsVideo()
    {
        Assert.True(PublicationTypeHelper.IsVideo(AppConstants.Media.BiblePublicationCodeVODMoviesModernDay));
        Assert.True(PublicationTypeHelper.IsVideo(AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes));
    }
}
