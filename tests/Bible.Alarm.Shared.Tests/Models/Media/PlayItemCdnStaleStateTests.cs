#nullable enable

using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PlayItemCdnStaleStateTests
{
    [Fact]
    public void Fresh_play_item_starts_with_recovery_flags_cleared()
    {
        var meta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "nwt",
            IsBibleContent = true,
            TrackCode = "1",
        };
        var sut = new PlayItem(meta, "https://cdn.example/a.mp3");

        Assert.False(sut.CdnStaleUrlRecoveryConsumed);
        Assert.False(sut.CdnStaleUrlRefetchReplayIssued);
        Assert.False(sut.StreamingOpenPhaseMediaFailedRetryDone);
    }
}
