using System.Collections.Generic;
using System.Reflection;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class JwSourceHelperTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("MakingMusic", false)]
    public void IsMusicPublicationCode_ReturnsExpected_ForExcludedOrEmpty(string? code, bool expected)
    {
        Assert.Equal(expected, JwSourceHelper.IsMusicPublicationCode(code));
    }

    [Theory]
    [InlineData(AppConstants.Media.MusicPublicationCodeOsg, true)]
    [InlineData(AppConstants.Media.MelodyMusicPublicationCodeIam, true)]
    [InlineData(AppConstants.Media.MediatorPublicationCodeVODConvMusic, true)]
    [InlineData(AppConstants.Media.MediatorCategoryKeyChildrenSongs, true)]
    public void IsMusicPublicationCode_ReturnsTrue_ForKnownMusicCodes(string code, bool expected)
    {
        Assert.Equal(expected, JwSourceHelper.IsMusicPublicationCode(code));
    }

    [Fact]
    public void IsMusicPublicationCode_TrimsWhitespace()
    {
        Assert.True(JwSourceHelper.IsMusicPublicationCode($"  {AppConstants.Media.MusicPublicationCodeOsg}  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetCanonicalMediatorPublicationCode_ReturnsNull_ForMissing(string? normalized)
    {
        Assert.Null(JwSourceHelper.GetCanonicalMediatorPublicationCode(normalized));
    }

    [Fact]
    public void GetCanonicalMediatorPublicationCode_MatchesIgnoringCase_ReturnsCanonicalCasing()
    {
        var canonical = JwSourceHelper.GetCanonicalMediatorPublicationCode("dramasgoodnews");
        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, canonical);

        canonical = JwSourceHelper.GetCanonicalMediatorPublicationCode("DRAMASGOODNEWS");
        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, canonical);
    }

    [Fact]
    public void GetCanonicalMediatorPublicationCode_ReturnsNull_WhenUnknown()
    {
        Assert.Null(JwSourceHelper.GetCanonicalMediatorPublicationCode("not-a-listed-mediator-code"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("nwt", false)]
    [InlineData(AppConstants.Media.BiblePublicationCodeDramasGoodNews, true)]
    [InlineData(AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes, true)]
    public void IsInDramasCategory_ReturnsExpected(string? publicationCode, bool expected)
    {
        Assert.Equal(expected, JwSourceHelper.IsInDramasCategory(publicationCode));
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("", "")]
    public void GetMediatorCategoryKey_ReturnsCodeOrEmpty(string publicationCode, string expected)
    {
        Assert.Equal(expected, JwSourceHelper.GetMediatorCategoryKey(publicationCode));
    }

    [Fact]
    public void GetMediatorCategoryKey_ReturnsEmpty_ForNull()
    {
        Assert.Equal(string.Empty, JwSourceHelper.GetMediatorCategoryKey(null!));
    }

    [Theory]
    [InlineData(201512, true)]
    [InlineData(209901, true)]
    [InlineData(185012, false)]
    [InlineData(209912, true)]
    [InlineData(209913, false)]
    [InlineData(201500, false)]
    [InlineData(201513, false)]
    [InlineData(201515, false)]
    public void LooksLikeIssueNumber_AppliesYmBounds(int trackNumber, bool expected)
    {
        Assert.Equal(expected, JwSourceHelper.LooksLikeIssueNumber(trackNumber));
    }

    [Fact]
    public void SectionCodesFrozenSets_Match_GetPubSpecialCaseCatalogs()
    {
        IEnumerable<string> issue = JwSourceHelper.SectionCodesUsingIssueParameter;
        IEnumerable<string> noParam = JwSourceHelper.SectionCodesSingleTrackNoParam;
        IEnumerable<string> zero = JwSourceHelper.SectionCodesSingleTrackZero;

        Assert.Contains("mwbv", issue);
        Assert.Contains("ivdd", noParam);
        Assert.Contains("bhat", zero);
    }

    [Fact]
    public void CategoryNameToCode_Normalizes_verbose_labels_with_and_and_spaces()
    {
        var method = typeof(JwSourceHelper).GetMethod(
            "CategoryNameToCode",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        Assert.Equal("BrochuresAndBooklets", method!.Invoke(null, ["Brochures and Booklets"]));
        Assert.Equal("MeetingsAndMinistry", method.Invoke(null, ["Meetings and Ministry"]));
        Assert.Equal("FaithAndBible", method.Invoke(null, ["Faith and Bible"]));
    }

    [Fact]
    public void GetCategoryName_ReturnsNull_ForNullOrEmptyPublicationCode()
    {
        Assert.Null(JwSourceHelper.GetCategoryName(null!));
        Assert.Null(JwSourceHelper.GetCategoryName(""));
    }

    [Fact]
    public void GetCategoryName_ReturnsBible_WhenPublicationIsNwt()
    {
        var name = JwSourceHelper.GetCategoryName(AppConstants.Media.BiblePublicationCodeNwt)
                   ?? JwSourceHelper.GetCategoryName(AppConstants.Media.BiblePublicationCodeNwt.ToUpperInvariant());
        Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, name);
    }

    [Fact]
    public void GetCategoryCode_NormalizesPublicationNameToDbForm()
    {
        Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, JwSourceHelper.GetCategoryCode(AppConstants.Media.BiblePublicationCodeNwt));

        Assert.Null(JwSourceHelper.GetCategoryCode("unknown-pub-xx"));
    }

    [Fact]
    public void GetPublicationCodesForCategory_ReturnsEmpty_ForMissingOrBlank()
    {
        Assert.Empty(JwSourceHelper.GetPublicationCodesForCategory(""));
        Assert.Empty(JwSourceHelper.GetPublicationCodesForCategory("Not aJWCategory"));
    }

    [Fact]
    public void GetPublicationCodesForCategory_ReturnsMembership_ForKnownCategory()
    {
        var codes = JwSourceHelper.GetPublicationCodesForCategory(AppConstants.Media.BiblePublicationCategoryBible);
        Assert.Contains(AppConstants.Media.BiblePublicationCodeNwt, codes);
        Assert.Contains(AppConstants.Media.BiblePublicationCodeBi12, codes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetCategoryCodesForPublication_ReturnsEmpty_ForMissing(string? publicationCode)
    {
        Assert.Empty(JwSourceHelper.GetCategoryCodesForPublication(publicationCode ?? string.Empty));
    }

    [Fact]
    public void GetCategoryCodesForPublication_IncludesMultipleCategories_WhenListedInOverlappingSets()
    {
        var codes = JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeSeriesBJFLessons);
        Assert.Contains("Children", codes);
        Assert.Contains("Series", codes);
    }

    [Fact]
    public void GetPublicationDisplayNameFallback_ReturnsNull_ForUnknown()
    {
        Assert.Null(JwSourceHelper.GetPublicationDisplayNameFallback(null));
        Assert.Null(JwSourceHelper.GetPublicationDisplayNameFallback(""));
        Assert.Null(JwSourceHelper.GetPublicationDisplayNameFallback("zzz"));
    }

    [Fact]
    public void GetPublicationDisplayNameFallback_ReturnsFriendlyNames_WhenMatched()
    {
        Assert.Equal(
            AppConstants.Media.PublicationDisplayNameKingdomMelodies,
            JwSourceHelper.GetPublicationDisplayNameFallback(AppConstants.Media.MelodyMusicPublicationCodeIam));

        Assert.Equal(
            AppConstants.Media.PublicationDisplayNameEnjoyLifeForeverVideos,
            JwSourceHelper.GetPublicationDisplayNameFallback(AppConstants.Media.NormalizedPublicationCodeVodLffVideosAd));

        Assert.Equal(
            AppConstants.Media.PublicationDisplayNameEnjoyLifeForeverVideos,
            JwSourceHelper.GetPublicationDisplayNameFallback(AppConstants.Media.NormalizedPublicationCodeBodLffVideosAd.ToUpperInvariant()));

        Assert.Equal(
            AppConstants.Media.PublicationDisplayNameDigForTreasuresInGodsWord,
            JwSourceHelper.GetPublicationDisplayNameFallback(AppConstants.Media.NormalizedPublicationCodeSeriesDigForTreasures));
    }

    [Fact]
    public void GetPublicationDisplayNameFallback_ReturnsBibleStoriesForLittleOnes_WhenSeriesBJFCode()
    {
        Assert.Equal(
            AppConstants.Media.PublicationDisplayNameBibleStoriesForLittleOnes,
            JwSourceHelper.GetPublicationDisplayNameFallback(AppConstants.Media.NormalizedPublicationCodeSeriesBJFLessons));
    }

    [Fact]
    public void BooksPublicationCodes_ContainsFlatMp3BookSample()
    {
        Assert.Contains("wcg", (IEnumerable<string>)JwSourceHelper.BooksPublicationCodes);
    }

    [Fact]
    public void WatchtowerAndAwakeMagazineCodes_SpanYearsFromMagazineHelper()
    {
        Assert.Contains($"w{MagazineHelper.MagazineStartYear}", (IEnumerable<string>)JwSourceHelper.WatchtowerMagazinePublicationCodes);
        Assert.Contains($"g{MagazineHelper.MagazineStartYear}", (IEnumerable<string>)JwSourceHelper.AwakeMagazinePublicationCodes);

        Assert.Contains($"w{MagazineHelper.MagazineEndYear}", (IEnumerable<string>)JwSourceHelper.WatchtowerMagazinePublicationCodes);
        Assert.Contains($"g{MagazineHelper.MagazineEndYear}", (IEnumerable<string>)JwSourceHelper.AwakeMagazinePublicationCodes);
    }

    [Fact]
    public void AllMediatorPublicationCodes_IncludesMediatorMusicAndFaithAndBibleSample()
    {
        var mediator = JwSourceHelper.AllMediatorPublicationCodes;

        Assert.Contains(AppConstants.Media.MediatorPublicationCodeVODConvMusic, mediator);
        Assert.Contains(AppConstants.Media.MediatorPublicationCodeVODBibleTeachings, mediator);
        Assert.DoesNotContain("wcg", mediator);
    }

    [Fact]
    public void AllPublicationCodesForEnglishSeeding_IncludesBibleAndVocal_ButOmitsMelodyInstrumentalPublication()
    {
        IEnumerable<string> all = JwSourceHelper.AllPublicationCodesForEnglishSeeding;
        Assert.Contains(AppConstants.Media.BiblePublicationCodeNwt, all);
        Assert.Contains(AppConstants.Media.MusicPublicationCodeOsg, all);

        IEnumerable<string> melodies = JwSourceHelper.MelodyMusicPublicationCodes;
        Assert.Contains(AppConstants.Media.MelodyMusicPublicationCodeIam, melodies);

        Assert.DoesNotContain(AppConstants.Media.MelodyMusicPublicationCodeIam, all);
    }

    [Fact]
    public void MediatorValidationExclusionCodes_IsEmpty()
    {
        Assert.Empty(JwSourceHelper.MediatorValidationExclusionCodes);
    }

    [Fact]
    public void CategoryToPublicationCodes_ExposesBiblePublicationCodesUnderBibleCategory()
    {
        var dict = JwSourceHelper.CategoryToPublicationCodes;
        Assert.True(dict.TryGetValue(AppConstants.Media.BiblePublicationCategoryBible, out var bibleCodes));
        Assert.Contains(AppConstants.Media.BiblePublicationCodeNwt, bibleCodes);
    }

    [Fact]
    public void BiblePublicationCodes_ContainsCoreEditions()
    {
        Assert.Contains(AppConstants.Media.BiblePublicationCodeNwt, JwSourceHelper.BiblePublicationCodes);
        Assert.Contains(AppConstants.Media.BiblePublicationCodeBi12, JwSourceHelper.BiblePublicationCodes);
    }
}
