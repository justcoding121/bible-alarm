#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationTrackListViewItemModelTests
{
    private static BiblePublication SamplePublication() =>
        new BiblePublication
        {
            Id = 1,
            PublicationCode = "nwtsty",
            Name = "Holy Scriptures",
            LanguageId = 1,
            Language = new Language
            {
                LanguageCode = "E",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            },
        };

    private static BiblePublicationTrack Mk(string code, string title, TrackUrl? url = null) =>
        new()
        {
            BiblePublicationId = 1,
            Publication = SamplePublication(),
            TrackCode = code,
            Title = title,
            TrackUrl = url,
        };

    [Fact]
    public void TrackCode_Title_decode_and_Url_reflect_track_state()
    {
        var sut = new BiblePublicationTrackListViewItemModel(Mk(
            code: "4",
            title: "P &amp; Q",
            url: new TrackUrl { Url = "https://streams.example/track" }));

        Assert.Equal("4", sut.TrackCode);
        Assert.Equal("P & Q", sut.Title);
        Assert.Equal("https://streams.example/track", sut.Url);
    }

    [Fact]
    public void Url_is_empty_when_track_has_no_linked_TrackUrl()
    {
        var sut = new BiblePublicationTrackListViewItemModel(Mk("1", "A"));

        Assert.Equal(string.Empty, sut.Url);
    }

    [Fact]
    public void CompareTo_orders_using_track_CodeComparison_semantics()
    {
        var two = new BiblePublicationTrackListViewItemModel(Mk("2", "b"));
        var ten = new BiblePublicationTrackListViewItemModel(Mk("10", "a"));

        Assert.True(two.CompareTo(ten) < 0);
    }

    [Fact]
    public void Operators_encode_numeric_track_order_relation()
    {
        var earlier = new BiblePublicationTrackListViewItemModel(Mk("2", ""));
        var later = new BiblePublicationTrackListViewItemModel(Mk("10", ""));

        Assert.True(earlier < later);
        Assert.False(earlier > later);
    }

    [Fact]
    public void Equals_reuses_EF_track_identity_when_identifier_nonzero()
    {
        var publication = SamplePublication();

        var leftPayload = Mk("same", "!");
        leftPayload.Publication = publication;
        leftPayload.Id = 42;

        var rightPayload = Mk("DIFF-CODE", "?");
        rightPayload.Publication = publication;
        rightPayload.Id = 42;

        var left = new BiblePublicationTrackListViewItemModel(leftPayload);
        var right = new BiblePublicationTrackListViewItemModel(rightPayload);

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Equals_falls_through_to_identity_when_EF_identifier_is_default()
    {
        var shared = Mk("same", "");

        Assert.True(new BiblePublicationTrackListViewItemModel(shared).Equals(new BiblePublicationTrackListViewItemModel(shared)));
    }

    [Fact]
    public void CompareTo_null_item_behaves_like_greater_than()
    {
        var sut = new BiblePublicationTrackListViewItemModel(Mk("x", "?"));

        Assert.Equal(1, sut.CompareTo((BiblePublicationTrackListViewItemModel?)null));
    }

    [Fact]
    public void Equals_object_unknown_runtime_type_returns_false()
    {
        var sut = new BiblePublicationTrackListViewItemModel(Mk("1", ""));

        Assert.False(sut.Equals(404));
    }
}
