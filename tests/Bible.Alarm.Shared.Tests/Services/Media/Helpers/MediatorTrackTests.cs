#nullable enable

using Bible.Alarm.Shared.Services.Media.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorTrackTests
{
    [Fact]
    public void Record_matches_on_track_title_and_url()
    {
        var a = new MediatorApiClient.MediatorTrack("12", "Episode", "https://cdn.example/a.mp3");
        var b = new MediatorApiClient.MediatorTrack("12", "Episode", "https://cdn.example/a.mp3");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Url_difference_breaks_equality()
    {
        var a = new MediatorApiClient.MediatorTrack("1", "T", "https://cdn.example/a.mp3");
        var b = new MediatorApiClient.MediatorTrack("1", "T", "https://cdn.example/b.mp3");

        Assert.NotEqual(a, b);
    }
}
