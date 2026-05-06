#nullable enable

using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackListViewItemModelTests
{
    private static MusicTrack Track(string code, string title, string? url = null) =>
        new()
        {
            TrackCode = code,
            Title = title ?? string.Empty,
            Url = url ?? string.Empty,
        };

    [Fact]
    public void TrackCode_and_Title_surface_track_helpers()
    {
        var model = new MusicTrackListViewItemModel(Track("12", "Song &#160;Title"));

        Assert.Equal("12", model.TrackCode);
        Assert.Equal("Song  Title", model.Title);
        Assert.Equal(string.Empty, model.Url);
    }

    [Fact]
    public void ToggleRepeatCommand_flips_repeat()
    {
        var model = new MusicTrackListViewItemModel(Track("1", "A"));

        Assert.False(model.Repeat);
        model.ToggleRepeatCommand.Execute(null);
        Assert.True(model.Repeat);
        model.ToggleRepeatCommand.Execute(null);
        Assert.False(model.Repeat);
    }

    [Fact]
    public void CompareTo_and_equality_use_track_code_ordering()
    {
        var a = new MusicTrackListViewItemModel(Track("2", "b"));
        var b = new MusicTrackListViewItemModel(Track("10", "a"));

        Assert.True(a.CompareTo(b) < 0);
        Assert.False(a.Equals(b));
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Operators_follow_comparison_contract()
    {
        var low = new MusicTrackListViewItemModel(Track("1", "x"));
        var high = new MusicTrackListViewItemModel(Track("9", "y"));

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= high);
        Assert.True(high >= low);
    }

    [Fact]
    public void Operators_when_left_track_code_sorts_after_right()
    {
        var left = new MusicTrackListViewItemModel(Track("10", "a"));
        var right = new MusicTrackListViewItemModel(Track("2", "b"));

        Assert.True(left > right);
        Assert.False(left < right);
        Assert.True(left >= right);
        Assert.False(left <= right);
    }

    [Fact]
    public void CompareTo_null_item_is_greater()
    {
        var a = new MusicTrackListViewItemModel(Track("1", "x"));

        Assert.Equal(1, a.CompareTo((MusicTrackListViewItemModel?)null));
        Assert.Equal(1, ((IComparable)a).CompareTo(null));
    }

    [Fact]
    public void CompareTo_object_non_item_delegates_like_null_other()
    {
        var a = new MusicTrackListViewItemModel(Track("5", "x"));

        Assert.Equal(1, ((IComparable)a).CompareTo(new object()));
    }

    [Fact]
    public void Equals_object_non_item_returns_false()
    {
        var a = new MusicTrackListViewItemModel(Track("3", "x"));

        Assert.False(a.Equals(123));
    }

    [Fact]
    public void IsNavigating_setter_updates_value()
    {
        var model = new MusicTrackListViewItemModel(Track("1", "x"));

        model.IsNavigating = true;

        Assert.True(model.IsNavigating);
    }
}
