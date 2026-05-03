#nullable enable

using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class MusicTrackLookupHelperTests
{
    private static Dictionary<int, MusicTrack> Tracks(params (int Key, string Code, string Title)[] items)
    {
        var dict = new Dictionary<int, MusicTrack>();
        foreach (var (key, code, title) in items)
        {
            dict[key] = new MusicTrack { TrackCode = code, Title = title };
        }

        return dict;
    }

    [Fact]
    public void TryGetByCode_ReturnsFalse_WhenNullOrBlankCodeOrEmptyDictionary()
    {
        var tracks = Tracks((0, "1", "A"));

        Assert.False(MusicTrackLookupHelper.TryGetByCode(null!, "1", out _));
        Assert.False(MusicTrackLookupHelper.TryGetByCode(tracks, (string?)null, out _));
        Assert.False(MusicTrackLookupHelper.TryGetByCode(tracks, "", out _));
        Assert.False(MusicTrackLookupHelper.TryGetByCode(tracks, "   ", out _));
        Assert.False(MusicTrackLookupHelper.TryGetByCode(new Dictionary<int, MusicTrack>(), "1", out _));
    }

    [Fact]
    public void TryGetByCode_ReturnsFirstMatch_UsingNumericCodeEquality()
    {
        var tracks = Tracks((10, "2", "Two"), (20, "01", "One"));

        Assert.True(MusicTrackLookupHelper.TryGetByCode(tracks, "1", out var one));
        Assert.Equal(20, one.Key);
        Assert.Equal("01", one.Track.TrackCode);

        Assert.True(MusicTrackLookupHelper.TryGetByCode(tracks, "002", out var two));
        Assert.Equal(10, two.Key);

        Assert.False(MusicTrackLookupHelper.TryGetByCode(tracks, "3", out _));
    }

    [Fact]
    public void TryGetByCode_MatchesAlphanumeric_WithOrdinalIgnoreCase()
    {
        var tracks = Tracks((1, "jwb-tag", "T"));

        Assert.True(MusicTrackLookupHelper.TryGetByCode(tracks, "JwB-TaG", out var hit));
        Assert.Equal(1, hit.Key);
    }

    [Fact]
    public void GetKeyByCode_ReturnsKeyOrNull_WrappingTryGet()
    {
        var tracks = Tracks((5, "10", "X"));

        Assert.Equal(5, MusicTrackLookupHelper.GetKeyByCode(tracks, "010"));
        Assert.Null(MusicTrackLookupHelper.GetKeyByCode(tracks, ""));
        Assert.Null(MusicTrackLookupHelper.GetKeyByCode(tracks, "z"));
    }

    [Fact]
    public void GetKeyByCode_ReturnsNull_WhenTracksNull()
    {
        Assert.Null(MusicTrackLookupHelper.GetKeyByCode(null!, "1"));
    }

    [Fact]
    public void TryGetByCode_FirstDictionaryEnumeration_WinsWhenMultipleEquivalentNumericCodesExist()
    {
        var tracks = Tracks((42, "1", "Older"), (7, "01", "Newer"));
        Assert.True(MusicTrackLookupHelper.TryGetByCode(tracks, "000001", out var hit));
        Assert.Equal(42, hit.Key);
        Assert.Equal("1", hit.Track.TrackCode);

        tracks = Tracks((7, "01", "Newer"), (42, "1", "Older"));
        Assert.True(MusicTrackLookupHelper.TryGetByCode(tracks, "1", out hit));
        Assert.Equal(7, hit.Key);
        Assert.Equal("01", hit.Track.TrackCode);
    }
}
