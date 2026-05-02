using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationSortHelperTests
{
    [Fact]
    public void SortByPriority_ReturnsEmpty_WhenPublicationsNull()
    {
        Assert.Empty(PublicationSortHelper.SortByPriority<BiblePublicationCodeItem>(
            null!,
            static p => p.Code));
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
