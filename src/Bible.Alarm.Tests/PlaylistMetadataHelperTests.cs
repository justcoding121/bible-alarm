#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaylistMetadataHelperTests
{
    private static TrackMetadata Meta(string pub, string? section, string trackCode) =>
        new()
        {
            PublicationCode = pub,
            SectionCode = section,
            TrackCode = trackCode,
            LookUpPath = "?t=1",
        };

    [Fact]
    public void No_op_when_section_missing_or_plain_book_code()
    {
        var a = Meta("nwt", null, "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(a);
        Assert.Null(a.DownloadCode);

        var b = Meta("nwt", "  ", "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(b);
        Assert.Null(b.DownloadCode);

        var c = Meta("nwt", "40", "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(c);
        Assert.Null(c.DownloadCode);
    }

    [Fact]
    public void No_op_when_section_does_not_match_publication_prefix()
    {
        var m = Meta("iam", "other-2", "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(m);
        Assert.Null(m.DownloadCode);
    }

    [Fact]
    public void Applies_download_code_and_original_track_for_disc_style_section()
    {
        var m = Meta("iam", "iam-2", "07");

        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(m);

        Assert.Equal("iam-2", m.DownloadCode);
        Assert.Equal(7, m.OriginalTrackCode);
    }

    [Fact]
    public void Rejects_suffix_that_is_all_zeros_or_non_numeric()
    {
        var zeros = Meta("iam", "iam-000", "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(zeros);
        Assert.Null(zeros.DownloadCode);

        var letters = Meta("iam", "iam-x", "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(letters);
        Assert.Null(letters.DownloadCode);
    }

    [Fact]
    public void Rejects_when_hyphen_segment_missing_suffix()
    {
        var m = Meta("iam", "iam-", "1");
        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(m);
        Assert.Null(m.DownloadCode);
    }
}
