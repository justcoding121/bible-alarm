using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationSortHelperTests
{
    [Fact]
    public void SortByPriority_returns_single_element_without_altering_identity()
    {
        IEnumerable<BiblePublicationCodeItem> pubs = [new("only", "Solo")];

        var ordered = PublicationSortHelper.SortByPriority(pubs, static p => p.Code, static p => p.Name).ToList();

        Assert.Single(ordered);
        Assert.Equal("only", ordered[0].Code);
    }

    [Fact]
    public void SortByPriorityForCategory_music_single_item_keeps_that_row()
    {
        IEnumerable<BiblePublicationCodeItem> pubs = [new(AppConstants.Media.MusicPublicationCodeSjjc, "Sing")];

        var ordered = PublicationSortHelper.SortByPriorityForCategory(
                pubs,
                static p => p.Code,
                static p => p.Name,
                AppConstants.Media.BiblePublicationCategoryMusic)
            .ToList();

        Assert.Single(ordered);
        Assert.Equal(AppConstants.Media.MusicPublicationCodeSjjc, ordered[0].Code);
    }

    [Fact]
    public void SortByPriority_DictionaryOverload_WithSingle_Returns_ordered_pair()
    {
        var dict = new Dictionary<string, string>
        {
            ["zzz"] = "Z",
        };

        var ordered = PublicationSortHelper.SortByPriority(dict);

        Assert.Single(ordered);
        Assert.Equal("zzz", ordered[0].Key);
    }

    [Fact]
    public void SortByPriority_ReturnsEmpty_WhenPublicationsNull()
    {
        Assert.Empty(PublicationSortHelper.SortByPriority<BiblePublicationCodeItem>(
            null!,
            static p => p.Code));
    }

    [Fact]
    public void SortByPriority_DictionaryOverload_ReturnsEmpty_WhenDictionaryNullOrEmpty()
    {
        Assert.Empty(PublicationSortHelper.SortByPriority<string>(null!));
        Assert.Empty(PublicationSortHelper.SortByPriority(new Dictionary<string, string>()));
    }

    [Fact]
    public void SortByPriority_when_getName_omitted_uses_code_for_name_tiebreak()
    {
        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new("rho", "Late alpha"),
            new("phi", "Early beta"),
        ];

        var ordered = PublicationSortHelper.SortByPriority(pubs, static p => p.Code).ToList();

        Assert.Equal(["phi", "rho"], ordered.Select(static o => o.Code).ToArray());
    }

    [Fact]
    public void SortByPriority_OrdersBibleEditionsThenByName()
    {
        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new("zzz", "Zeta"),
            new(AppConstants.Media.BiblePublicationCodeBi12, "B"),
            new(AppConstants.Media.BiblePublicationCodeNwt, "A")
        ];

        var ordered = PublicationSortHelper.SortByPriority(pubs, static p => p.Code, static p => p.Name).ToList();

        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, ordered[0].Code);
        Assert.Equal(AppConstants.Media.BiblePublicationCodeBi12, ordered[1].Code);
        Assert.Equal("zzz", ordered[2].Code);
    }

    [Fact]
    public void SortByPriority_DictionaryOverload_ReturnsEmpty_ForNullOrEmpty()
    {
        Assert.Empty(PublicationSortHelper.SortByPriority<string>(null!));
        Assert.Empty(PublicationSortHelper.SortByPriority(new Dictionary<string, string>()));
    }

    [Fact]
    public void SortByPriority_DictionaryOverload_SortsKeysLikePublicationComparer()
    {
        var dict = new Dictionary<string, string>
        {
            ["zzz"] = "Z",
            [AppConstants.Media.BiblePublicationCodeBi12] = "B",
            [AppConstants.Media.BiblePublicationCodeNwt] = "A",
        };

        var ordered = PublicationSortHelper.SortByPriority(dict);

        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, ordered[0].Key);
        Assert.Equal(AppConstants.Media.BiblePublicationCodeBi12, ordered[1].Key);
    }

    [Fact]
    public void SortByPriority_Dictionary_when_code_priority_tie_breaks_via_get_name_display_order()
    {
        var dict = new Dictionary<string, string>
        {
            ["002"] = "ZZZ",
            ["2"] = "AAA",
        };

        var withDisplayNameSecondary = PublicationSortHelper
            .SortByPriority(dict, static v => v)
            .Select(static kvp => kvp.Key)
            .ToList();

        Assert.Equal(["2", "002"], withDisplayNameSecondary);

        var keyOnlySecondary = PublicationSortHelper.SortByPriority(dict).Select(static kvp => kvp.Key).ToList();

        Assert.Equal(["002", "2"], keyOnlySecondary);
    }

    [Fact]
    public void SortByPriorityForCategory_ReturnsEmpty_WhenInputNull()
    {
        Assert.Empty(PublicationSortHelper.SortByPriorityForCategory<BiblePublicationCodeItem>(
            null!,
            static p => p.Code,
            static p => p.Name,
            AppConstants.Media.BiblePublicationCategoryMusic));
    }

    [Fact]
    public void SortByPriorityForCategory_MusicPrioritizesOsgBeforeOtherCodes()
    {
        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new(AppConstants.Media.MusicPublicationCodeSjjc, "Sing"),
            new(AppConstants.Media.MusicPublicationCodeOsg, "Original"),
        ];

        var ordered = PublicationSortHelper.SortByPriorityForCategory(
                pubs,
                static p => p.Code,
                static p => p.Name,
                AppConstants.Media.BiblePublicationCategoryMusic)
            .ToList();

        Assert.Equal(AppConstants.Media.MusicPublicationCodeOsg, ordered[0].Code);
    }

    [Fact]
    public void SortByPriorityForCategory_when_getName_omitted_uses_code_for_name_tiebreak_with_null_category()
    {
        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new("zeta", "B"),
            new("alfa", "A"),
        ];

        var ordered = PublicationSortHelper.SortByPriorityForCategory(
                pubs,
                static p => p.Code,
                getName: null,
                categoryName: null)
            .ToList();

        Assert.Equal(["alfa", "zeta"], ordered.Select(static o => o.Code).ToArray());
    }

    [Fact]
    public void SortByPriorityForCategory_WatchtowerMagazine_orders_newer_year_first_for_display()
    {
        const string newer = "w2026";
        const string older = "w2015";

        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new(older, "older"),
            new(newer, "newer"),
        ];

        var ordered = PublicationSortHelper.SortByPriorityForCategory(
                pubs,
                static p => p.Code,
                static p => p.Name,
                AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine)
            .ToList();

        Assert.Equal(newer, ordered[0].Code);
        Assert.Equal(older, ordered[1].Code);
    }

    [Fact]
    public void SortByPriorityForCategory_trims_whitespace_when_selecting_music_comparer()
    {
        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new(AppConstants.Media.MusicPublicationCodeSjjc, "Sing"),
            new(AppConstants.Media.MusicPublicationCodeOsg, "Original"),
        ];

        var ordered = PublicationSortHelper.SortByPriorityForCategory(
                pubs,
                static p => p.Code,
                static p => p.Name,
                $" \t {AppConstants.Media.BiblePublicationCategoryMusic}{Environment.NewLine}")
            .ToList();

        Assert.Equal(AppConstants.Media.MusicPublicationCodeOsg, ordered[0].Code);
    }

    [Fact]
    public void SortByPriorityForCategory_BibleCategory_matches_publication_priority_and_name_tiebreak()
    {
        IEnumerable<BiblePublicationCodeItem> pubs =
        [
            new(AppConstants.Media.BiblePublicationCodeBi12, "B"),
            new("zzz", "Z"),
            new(AppConstants.Media.BiblePublicationCodeNwt, "A"),
        ];

        var ordered = PublicationSortHelper.SortByPriorityForCategory(
                pubs,
                static p => p.Code,
                static p => p.Name,
                AppConstants.Media.BiblePublicationCategoryBible)
            .ToList();

        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, ordered[0].Code);
        Assert.Equal(AppConstants.Media.BiblePublicationCodeBi12, ordered[1].Code);
        Assert.Equal("zzz", ordered[2].Code);
    }

    [Fact]
    public void GetPublicationSortPriority_DelegatesToPublicationCodeHelper()
    {
        Assert.Equal(
            PublicationCodeHelper.GetPublicationSortPriority(AppConstants.Media.BiblePublicationCodeNwt),
            PublicationSortHelper.GetPublicationSortPriority(AppConstants.Media.BiblePublicationCodeNwt));
    }

    [Fact]
    public void GetPriorityPublicationCodes_DelegatesToPublicationCodeHelper()
    {
        Assert.Equal(
            PublicationCodeHelper.GetPriorityPublicationCodes(),
            PublicationSortHelper.GetPriorityPublicationCodes());
    }

    private readonly record struct BiblePublicationCodeItem(string Code, string Name);
}
