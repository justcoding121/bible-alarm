using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationCodeHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsNull_ForWhitespaceOrEmpty(string? code)
    {
        Assert.Null(PublicationCodeHelper.Normalize(code));
    }

    [Fact]
    public void Normalize_TrimsLeadingAndTrailingWhitespace()
    {
        Assert.Equal("nwt", PublicationCodeHelper.Normalize("  nwt  "));
    }

    [Fact]
    public void PublicationCodeComparer_OrderPutsNullLast()
    {
        var cmp = PublicationCodeHelper.PublicationCodeComparer;
        Assert.True(cmp.Compare(null, AppConstants.Media.BiblePublicationCodeNwt) > 0);
        Assert.True(cmp.Compare(AppConstants.Media.BiblePublicationCodeNwt, null) < 0);
        Assert.Equal(0, cmp.Compare(null, null));
    }

    [Fact]
    public void PublicationCodeComparer_OrdersBibleEditionPriority()
    {
        var cmp = PublicationCodeHelper.PublicationCodeComparer;
        Assert.True(
            cmp.Compare(AppConstants.Media.BiblePublicationCodeNwt, AppConstants.Media.BiblePublicationCodeBi12) < 0);
        Assert.True(
            cmp.Compare(AppConstants.Media.BiblePublicationCodeBi12, AppConstants.Media.BiblePublicationCodeNwt) > 0);
    }

    [Theory]
    [InlineData(null, AppConstants.Media.MusicPublicationCodeOsg)]
    [InlineData("  ", AppConstants.Media.MusicPublicationCodeOsg)]
    public void PublicationCodeComparer_PutsWhitespaceBeforeNonempty(string? whitespace, string other)
    {
        var bibleCmp = PublicationCodeHelper.PublicationCodeComparer;
        Assert.True(bibleCmp.Compare(whitespace, other) > 0);
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_Music_PrioritizesOsg()
    {
        var cmp = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryMusic);
        Assert.True(cmp.Compare(AppConstants.Media.MusicPublicationCodeOsg, AppConstants.Media.MusicPublicationCodeSjjc) < 0);
        Assert.True(cmp.Compare(AppConstants.Media.MusicPublicationCodeSjjc, AppConstants.Media.MusicPublicationCodeOsg) > 0);
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_BibleCategory_matches_PublicationCodeComparer()
    {
        var cat = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryBible);
        var bibleDefault = PublicationCodeHelper.PublicationCodeComparer;

        Assert.Equal(
            bibleDefault.Compare(AppConstants.Media.BiblePublicationCodeNwt, AppConstants.Media.BiblePublicationCodeBi12),
            cat.Compare(AppConstants.Media.BiblePublicationCodeNwt, AppConstants.Media.BiblePublicationCodeBi12));
        Assert.Equal(
            bibleDefault.Compare(null, AppConstants.Media.BiblePublicationCodeNwt),
            cat.Compare(null, AppConstants.Media.BiblePublicationCodeNwt));
        Assert.Equal(
            bibleDefault.Compare("zzz", "aab"),
            cat.Compare("zzz", "aab"));
    }

    [Fact]
    public void GetNavigationComparerForCategory_non_magazine_categories_match_display_comparer()
    {
        var nav = PublicationCodeHelper.GetNavigationComparerForCategory(AppConstants.Media.BiblePublicationCategoryBible);
        var display = PublicationCodeHelper.GetPublicationCodeComparerForCategory(AppConstants.Media.BiblePublicationCategoryBible);

        Assert.Equal(
            display.Compare("z", "a"),
            nav.Compare("z", "a"));
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_Magazine_sorts_recognized_code_before_non_magazine_string()
    {
        var cmp = PublicationCodeHelper.GetPublicationCodeComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);
        var mag = $"w{MagazineHelper.MagazineStartYear + 3}";

        Assert.True(cmp.Compare(mag, "plain") < 0);
        Assert.True(cmp.Compare("plain", mag) > 0);
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_Magazine_when_neither_code_is_magazine_uses_string_ordering()
    {
        var cmp = PublicationCodeHelper.GetPublicationCodeComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);

        Assert.True(cmp.Compare("apple", "banana") < 0);
    }

    [Fact]
    public void GetNavigationComparerForCategory_trims_whitespace_before_magazine_branch()
    {
        var padded = $"  {AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine}\t";
        var nav = PublicationCodeHelper.GetNavigationComparerForCategory(padded);
        var display = PublicationCodeHelper.GetPublicationCodeComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);
        var yOld = $"w{MagazineHelper.MagazineStartYear}";
        var yNew = $"w{MagazineHelper.MagazineStartYear + 5}";

        Assert.NotEqual(nav.Compare(yOld, yNew), display.Compare(yOld, yNew));
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_Magazines_SortsDescendingByYearWhenBothRecognized()
    {
        var wt = PublicationCodeHelper.GetPublicationCodeComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);
        var yearA = $"w{MagazineHelper.MagazineStartYear + 10}";
        var yearB = $"w{MagazineHelper.MagazineStartYear}";
        Assert.True(wt.Compare(yearA, yearB) < 0);
        Assert.True(wt.Compare(yearB, yearA) > 0);
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_AwakeMagazine_SortsDescendingByYearWhenBothRecognized()
    {
        var awake = PublicationCodeHelper.GetPublicationCodeComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryAwakeMagazine);
        var yearA = $"g{MagazineHelper.MagazineStartYear + 10}";
        var yearB = $"g{MagazineHelper.MagazineStartYear}";
        Assert.True(awake.Compare(yearA, yearB) < 0);
        Assert.True(awake.Compare(yearB, yearA) > 0);
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_Unknown_IsOrdinalFallback()
    {
        var cmp = PublicationCodeHelper.GetPublicationCodeComparerForCategory("SomeFutureCategory");
        Assert.True(cmp.Compare("b", "a") > 0);
    }

    [Fact]
    public void GetPublicationCodeComparerForCategory_NullTreatsLikeUnknown()
    {
        var bible = PublicationCodeHelper.GetPublicationCodeComparerForCategory(null);
        Assert.Equal(
            PublicationCodeHelper.GetPublicationCodeComparerForCategory("SomeFutureCategory").Compare("z", "a"),
            bible.Compare("z", "a"));
    }

    [Fact]
    public void GetNavigationComparerForCategory_Magazine_DoesNotForceDescendingYears()
    {
        var nav = PublicationCodeHelper.GetNavigationComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);
        var display = PublicationCodeHelper.GetPublicationCodeComparerForCategory(
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine);
        var yOld = $"w{MagazineHelper.MagazineStartYear}";
        var yNew = $"w{MagazineHelper.MagazineStartYear + 5}";
        Assert.NotEqual(nav.Compare(yOld, yNew), display.Compare(yOld, yNew));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("nwt ", " NWT ", true)]
    [InlineData("01", "1", true)]
    [InlineData("nwt", "bi12", false)]
    public void CodeEquals_UsesNormalizationAndNumericRules(string? a, string? b, bool expected)
    {
        Assert.Equal(expected, PublicationCodeHelper.CodeEquals(a, b));
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData("nwt", 0)]
    [InlineData("NWT", 0)]
    [InlineData("bi12", 1)]
    [InlineData("other", 2)]
    public void GetPublicationSortPriority_MatchesBiblePriorityTable(string? code, int expectedIndex)
    {
        Assert.Equal(expectedIndex, PublicationCodeHelper.GetPublicationSortPriority(code));
    }

    [Fact]
    public void GetPriorityPublicationCodes_ReturnsClone()
    {
        var first = PublicationCodeHelper.GetPriorityPublicationCodes();
        var second = PublicationCodeHelper.GetPriorityPublicationCodes();
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }
}
