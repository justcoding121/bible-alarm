#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Reducers;

namespace Bible.Alarm.Tests;

public sealed class SetDefaultScheduleMetadataActionTests
{
    [Fact]
    public void PlaybackReducer_OnSetDefaultScheduleMetadata_updates_defaults_slice()
    {
        var prior = new PlaybackState();

        var next = PlaybackReducer.OnSetDefaultScheduleMetadata(prior, new SetDefaultScheduleMetadataAction
        {
            ScheduleId = 12,
            Title = "Morning",
            Artist = "Speaker",
            Album = "Series",
            ArtworkUrl = "https://cover",
        });

        Assert.Equal(12, next.DefaultScheduleId);
        Assert.Equal("Morning", next.DefaultScheduleTitle);
        Assert.Equal("Speaker", next.DefaultScheduleArtist);
        Assert.Equal("Series", next.DefaultScheduleAlbum);
        Assert.Equal("https://cover", next.DefaultScheduleArtworkUrl);
    }
}
