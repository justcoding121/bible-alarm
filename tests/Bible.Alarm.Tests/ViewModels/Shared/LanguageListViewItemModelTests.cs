#nullable enable

using System;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class LanguageListViewItemModelTests
{
    private static Language Lang(string code) =>
        new() { LanguageCode = code, Direction = AppConstants.Media.TextDirectionLeftToRight };

    [Fact]
    public void Constructor_maps_code_direction_and_initial_display_Name()
    {
        var sut = new LanguageListViewItemModel(Lang("E"), displayName: "English");

        Assert.Equal("E", sut.Code);
        Assert.Equal(AppConstants.Media.TextDirectionLeftToRight, sut.Direction);
        Assert.Equal("English", sut.Name);
    }

    [Fact]
    public void DownloadProgress_clamping_matches_publication_rows()
    {
        var sut = new LanguageListViewItemModel(Lang("X"), "?");

        sut.DownloadProgress = 1.01;
        Assert.Equal(1.0, sut.DownloadProgress);

        sut.DownloadProgress = -5;
        Assert.Equal(-1.0, sut.DownloadProgress);
    }

    [Fact]
    public void DownloadProgressText_shows_percentage_when_progress_non_negative()
    {
        var sut = new LanguageListViewItemModel(Lang("F"), "?");

        sut.DownloadProgress = 0.33;

        Assert.Equal("33%", sut.DownloadProgressText);
    }

    [Fact]
    public void CompareTo_orders_by_Name_ordinal()
    {
        var french = new LanguageListViewItemModel(Lang("F"), "Français");
        var esperanto = new LanguageListViewItemModel(Lang("Eo"), "Esperanto");

        Assert.True(french.CompareTo(esperanto) > 0);
    }

    [Fact]
    public void Equality_uses_display_Name_only()
    {
        var a = new LanguageListViewItemModel(Lang("a"), "Same Label");
        var b = new LanguageListViewItemModel(Lang("b"), "Same Label");

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(new LanguageListViewItemModel(Lang("a"), "Other")));
    }

    [Fact]
    public void Ordering_operators_respect_Name_sort()
    {
        var low = new LanguageListViewItemModel(Lang("1"), "A");
        var high = new LanguageListViewItemModel(Lang("2"), "Z");

        Assert.True(low < high);
        Assert.True(high > low);
    }

    [Fact]
    public void CompareTo_generic_null_is_greater()
    {
        var sut = new LanguageListViewItemModel(Lang("E"), "?");

        Assert.Equal(1, sut.CompareTo((LanguageListViewItemModel?)null));
    }

    [Fact]
    public void CompareTo_object_non_Language_delegates_to_null_branch()
    {
        var sut = new LanguageListViewItemModel(Lang("E"), "?");

        Assert.Equal(1, ((IComparable)sut).CompareTo(new object()));
    }
}
