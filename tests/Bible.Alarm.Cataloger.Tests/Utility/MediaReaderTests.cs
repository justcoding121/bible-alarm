#nullable enable

using Bible.Alarm.Cataloger.Utility;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class MediaReaderTests
{
    [Fact]
    public async Task GetMelodyMusicTracksByDisc_returns_empty_when_publication_directory_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "bible-alarm-media-index-" + Guid.NewGuid());
        var sut = new MediaReader(root);

        var result = await sut.GetMelodyMusicTracksByDisc("IAM");

        Assert.Empty(result);
    }
}
