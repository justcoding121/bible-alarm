#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionListViewItemModelTests
{
    [Fact]
    public void SectionCode_and_Name_decode_HTML_from_section_entity()
    {
        var section = new BiblePublicationSection { SectionCode = "gen", Name = "Genesis&#160;1" };

        var sut = new BiblePublicationSectionListViewItemModel(section);

        Assert.Same(section, sut.Section);
        Assert.Equal("gen", sut.SectionCode);
        Assert.Equal("Genesis 1", sut.Name);
    }

    [Fact]
    public void DownloadProgress_clamps_to_one_and_truncates_below_negative_one_sentinel()
    {
        var sut = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection());

        sut.DownloadProgress = 2;
        Assert.Equal(1.0, sut.DownloadProgress);

        sut.DownloadProgress = -99;
        Assert.Equal(-1.0, sut.DownloadProgress);
    }

    [Fact]
    public void DownloadProgressText_empty_below_zero_progress()
    {
        var sut = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection());

        sut.DownloadProgress = -0.001;

        Assert.Equal(string.Empty, sut.DownloadProgressText);
    }

    [Fact]
    public void CompareTo_uses_numeric_section_code_ordering_when_applicable()
    {
        var two = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "2" });
        var ten = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "10" });

        Assert.True(two.CompareTo(ten) < 0);
    }

    [Fact]
    public void Equals_matches_on_section_code_only()
    {
        var left = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "mat", Name = "A" });
        var right = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "MAT", Name = "B" });

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Operators_encode_strict_order_relation()
    {
        var earlier = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1" });
        var later = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "2" });

        Assert.True(earlier < later);
        Assert.False(earlier > later);
    }

    [Fact]
    public void CompareTo_null_item_is_ordered_greater()
    {
        var sut = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1" });

        Assert.Equal(1, sut.CompareTo((BiblePublicationSectionListViewItemModel?)null));
    }

    [Fact]
    public void Equals_object_round_trips_through_section_Model()
    {
        var sut = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1" });

        Assert.False(sut.Equals(3));
        Assert.True(sut.Equals(new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1" })));
    }

    [Fact]
    public void GetHashCode_matches_case_insensitive_section_code_semantics()
    {
        var a = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "Exo" });
        var b = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "exo" });

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
