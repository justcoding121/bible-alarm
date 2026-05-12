#nullable enable

using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class TrackMetadataTests
{
    [Fact]
    public void PlayType_is_Bible_when_IsBibleContent_true()
    {
        var sut = new TrackMetadata { IsBibleContent = true };

        Assert.Equal(PlayType.Bible, sut.PlayType);
    }

    [Fact]
    public void PlayType_is_Music_when_IsBibleContent_false()
    {
        var sut = new TrackMetadata { IsBibleContent = false };

        Assert.Equal(PlayType.Music, sut.PlayType);
    }

    [Fact]
    public void IsAlarmMusic_is_inverse_of_IsBibleContent()
    {
        var bible = new TrackMetadata { IsBibleContent = true };
        var music = new TrackMetadata { IsBibleContent = false };

        Assert.False(bible.IsAlarmMusic);
        Assert.True(music.IsAlarmMusic);
    }

    [Fact]
    public void LookUpPath_get_throws_when_index_path_never_set()
    {
        var sut = new TrackMetadata();

        Assert.Throws<InvalidOperationException>(() => _ = sut.LookUpPath);
    }

    [Fact]
    public void TryGetLookUpPath_returns_false_when_lookup_never_assigned()
    {
        var sut = new TrackMetadata();

        var ok = sut.TryGetLookUpPath(out var path);

        Assert.False(ok);
        Assert.Null(path);
    }

    [Fact]
    public void TryGetLookUpPath_returns_false_when_lookup_cleared()
    {
        var sut = new TrackMetadata { LookUpPath = "indexed" };
        sut.LookUpPath = null!;

        var ok = sut.TryGetLookUpPath(out var path);

        Assert.False(ok);
        Assert.Null(path);
    }

    [Fact]
    public void TryGetLookUpPath_returns_false_when_lookup_empty_string()
    {
        var sut = new TrackMetadata { LookUpPath = string.Empty };

        var ok = sut.TryGetLookUpPath(out var path);

        Assert.False(ok);
        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void TryGetLookUpPath_returns_true_and_path_when_non_empty()
    {
        var sut = new TrackMetadata { LookUpPath = "/media?k=1" };

        var ok = sut.TryGetLookUpPath(out var path);

        Assert.True(ok);
        Assert.Equal("/media?k=1", path);
    }

    [Fact]
    public void TryGetLookUpPath_returns_true_for_whitespace_only_payload()
    {
        var sut = new TrackMetadata { LookUpPath = "   " };

        var ok = sut.TryGetLookUpPath(out var path);

        Assert.True(ok);
        Assert.Equal("   ", path);
    }
}
