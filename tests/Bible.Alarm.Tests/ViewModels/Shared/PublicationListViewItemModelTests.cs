#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class PublicationListViewItemModelTests
{
    [Fact]
    public void Name_decodes_HTML_when_publication_has_distinct_display_name()
    {
        var pub = new Publication { Name = "A &amp; B", PublicationCode = "bk" };

        var sut = new PublicationListViewItemModel(pub);

        Assert.Equal("A & B", sut.Name);
    }

    [Fact]
    public void Name_replaces_equals_code_publication_with_JW_fallback_display_name()
    {
        var sut = new PublicationListViewItemModel(new Publication
        {
            Name = AppConstants.Media.MelodyMusicPublicationCodeIam.ToUpperInvariant(),
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
        });

        Assert.Equal(AppConstants.Media.PublicationDisplayNameKingdomMelodies, sut.Name);
    }

    [Fact]
    public void Name_preserves_whitespace_when_blank_branch_has_no_fallback_coalescing_keeps_raw_name()
    {
        var sut = new PublicationListViewItemModel(new Publication { Name = "   ", PublicationCode = "zz-unknown" });

        Assert.Equal("   ", sut.Name);
    }

    [Fact]
    public void PublicationLanguageCode_reflects_linked_language_on_BiblePublication()
    {
        var bible = new BiblePublication
        {
            PublicationCode = "nwt",
            Name = "Holy Scriptures",
            LanguageId = 1,
            Language = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
        };

        var sut = new PublicationListViewItemModel(bible);

        Assert.Equal("E", sut.PublicationLanguageCode);
        Assert.False(sut.IsPublicationWithoutLanguage);
        Assert.True(sut.HasLanguageId);
    }

    [Fact]
    public void IsPublicationWithoutLanguage_true_when_BiblePublication_has_null_LanguageId()
    {
        var bible = new BiblePublication
        {
            PublicationCode = "iam",
            Name = "Music",
            LanguageId = null,
        };

        var sut = new PublicationListViewItemModel(bible);

        Assert.True(sut.IsPublicationWithoutLanguage);
        Assert.False(sut.HasLanguageId);
        Assert.Null(sut.PublicationLanguageCode);
    }

    [Fact]
    public void DownloadProgress_clamps_high_values_and_keeps_negative_one_sentinel()
    {
        var sut = new PublicationListViewItemModel(new Publication { Name = "X", PublicationCode = "x" });

        sut.DownloadProgress = -1;
        sut.DownloadProgress = 9;
        Assert.Equal(1.0, sut.DownloadProgress);

        sut.DownloadProgress = -1;
        sut.DownloadProgress = -2;
        Assert.Equal(-1.0, sut.DownloadProgress);
    }

    [Fact]
    public void DownloadProgressText_empty_when_sentinel_negative_one()
    {
        var sut = new PublicationListViewItemModel(new Publication { PublicationCode = "p" });

        sut.DownloadProgress = -1;

        Assert.Equal(string.Empty, sut.DownloadProgressText);
    }

    [Fact]
    public void DownloadProgressText_formats_half_as_fifty_percent()
    {
        var sut = new PublicationListViewItemModel(new Publication { PublicationCode = "p" });

        sut.DownloadProgress = 0.505;

        Assert.Equal("51%", sut.DownloadProgressText);
    }

    [Fact]
    public void CompareTo_orders_by_publication_code_then_by_name_case_insensitive()
    {
        var a = new PublicationListViewItemModel(new Publication { Name = "Zed", PublicationCode = "bk" });
        var b = new PublicationListViewItemModel(new Publication { Name = "Alpha", PublicationCode = "bk" });

        Assert.True(a.CompareTo(b) > 0);
    }

    [Fact]
    public void Equals_requires_matching_code_and_language_code_case_insensitive()
    {
        var lang = new Language { LanguageCode = "e-us", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var left = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "OSG",
            Name = "Songs",
            LanguageId = 1,
            Language = lang,
        });
        var right = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "osg",
            Name = "Other",
            LanguageId = 2,
            Language = new Language { LanguageCode = "E-US", Direction = AppConstants.Media.TextDirectionLeftToRight },
        });

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Operators_follow_comparison_contract_for_ordered_pair()
    {
        var low = new PublicationListViewItemModel(new Publication { Name = "A", PublicationCode = "aa" });
        var high = new PublicationListViewItemModel(new Publication { Name = "B", PublicationCode = "bb" });

        Assert.True(low < high);
        Assert.True(high > low);
    }

    [Fact]
    public void CompareTo_object_wrong_runtime_type_behaves_like_null_other()
    {
        var sut = new PublicationListViewItemModel(new Publication { Name = "", PublicationCode = "p" });

        Assert.Equal(1, sut.CompareTo(new object()));
    }
}
