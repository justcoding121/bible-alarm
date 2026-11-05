using Bible.Alarm.Shared.Media;

namespace Bible.Alarm.Shared.Tests.Media;

[Trait("Platform", "Shared")]
public sealed class MediaSessionMappingTests
{
    [Theory]
    [InlineData("12", 12)]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    [InlineData("-3", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("bible", 0)]
    public void ParseScheduleId_keeps_positive_integers_only(string? mediaId, int expected)
    {
        Assert.Equal(expected, MediaSessionMediaId.ParseScheduleId(mediaId));
    }

    [Theory]
    [InlineData(ProxyPlaybackKind.Playing, "READY", true, false)]
    [InlineData(ProxyPlaybackKind.Buffering, "BUFFERING", true, false)]
    [InlineData(ProxyPlaybackKind.Paused, "READY", false, false)]
    [InlineData(ProxyPlaybackKind.Error, "IDLE", false, true)]
    [InlineData(ProxyPlaybackKind.None, "IDLE", false, false)]
    public void Playback_mapping_matches_the_car_rules(
        ProxyPlaybackKind kind,
        string stateName,
        bool playWhenReady,
        bool hasPlayerError)
    {
        var mapped = ProxyPlaybackMapping.For(kind);

        Assert.Equal(stateName, mapped.StateName);
        Assert.Equal(playWhenReady, mapped.PlayWhenReady);
        Assert.Equal(hasPlayerError, mapped.HasPlayerError);
    }
}
