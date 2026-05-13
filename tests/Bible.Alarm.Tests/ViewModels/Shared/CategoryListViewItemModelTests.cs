#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class CategoryListViewItemModelTests
{
    private static Category Cat(int id = 9, string code = "Books") =>
        new()
        {
            Id = id,
            CategoryCode = code,
        };

    [Fact]
    public void Constructor_sets_fields_and_display_name()
    {
        var row = Cat();
        var sut = new CategoryListViewItemModel(row, displayName: "Books (EN)");

        Assert.Equal(row.Id, sut.Id);
        Assert.Equal(row.CategoryCode, sut.CategoryCode);
        Assert.Equal("Books (EN)", sut.Name);
    }

    [Fact]
    public void Constructor_falls_back_to_category_code_when_display_name_null()
    {
        var row = Cat(code: "Music");
        var sut = new CategoryListViewItemModel(row, displayName: null);
        Assert.Equal("Music", sut.Name);
    }

    [Fact]
    public void DownloadProgressText_empty_when_sentinel_not_set()
    {
        var sut = new CategoryListViewItemModel(Cat());

        sut.DownloadProgress = -1;

        Assert.Equal(string.Empty, sut.DownloadProgressText);
    }

    [Fact]
    public void DownloadProgress_clamps_percent_and_reflects_Text()
    {
        var sut = new CategoryListViewItemModel(Cat());

        sut.DownloadProgress = -1.25;
        Assert.Equal(-1.0, sut.DownloadProgress);
        Assert.Equal(string.Empty, sut.DownloadProgressText);

        sut.DownloadProgress = 1.05;
        Assert.Equal(1.0, sut.DownloadProgress);
        Assert.Equal("100%", sut.DownloadProgressText);

        sut.DownloadProgress = 0.456;
        Assert.Equal("46%", sut.DownloadProgressText);
    }

    [Fact]
    public void CompareTo_and_Equality_follow_Name_ordinal_case_sensitive()
    {
        var a = new CategoryListViewItemModel(Cat(), "Bee");
        var match = new CategoryListViewItemModel(Cat(), "Bee");

        Assert.Equal(0, a.CompareTo(match));
        Assert.True(a.Equals(match));

        Assert.Equal(1, a.CompareTo(null));
        Assert.Equal(1, a.CompareTo(new object()));
        Assert.False(a.Equals(new object()));

        Assert.NotEqual(0, a.CompareTo(new CategoryListViewItemModel(Cat(), "bee")));
    }

    [Fact]
    public void Operators_require_non_null_operands_for_relational_comparison()
    {
        var a = new CategoryListViewItemModel(Cat(), "One");
        Assert.False(a < null!);
        Assert.False(null! < a);
    }
}
