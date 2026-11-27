#nullable enable

using Bible.Alarm.UITests.Infrastructure;

namespace Bible.Alarm.UITests.Tests.Infrastructure;

public sealed class MediaSessionDumpTests
{
    [Fact]
    public void Parse_reads_playing_session_for_the_package()
    {
        const string dump = """
            Media Sessions:
              com.jthomas.info.Bible.Alarm/MediaSession
                package=com.jthomas.info.Bible.Alarm
                state=PlaybackState {state=3, position=1200, buffered=0}
                metadata: description=Genesis 1, artist=Bible
              com.example.other/MediaSession
                package=com.example.other
                state=PlaybackState {state=2, position=0}
            """;

        var snapshot = MediaSessionDump.Parse(dump, "com.jthomas.info.Bible.Alarm");

        Assert.Equal(1, snapshot.SessionCount);
        Assert.Equal(3, snapshot.PlaybackState);
        Assert.Equal("Genesis 1", snapshot.Title);
    }

    [Fact]
    public void Parse_reads_the_named_playback_state_used_by_current_android()
    {
        const string dump = """
            package=com.jthomas.info.Bible.Alarm
            state=PlaybackState {state=PAUSED(2), position=10}
            """;

        var snapshot = MediaSessionDump.Parse(dump, "com.jthomas.info.Bible.Alarm");

        Assert.Equal(2, snapshot.PlaybackState);
    }

    [Fact]
    public void Parse_returns_empty_when_the_package_is_absent()
    {
        var snapshot = MediaSessionDump.Parse("package=com.example.other\nstate=2", "com.jthomas.info.Bible.Alarm");

        Assert.Equal(0, snapshot.SessionCount);
        Assert.Null(snapshot.PlaybackState);
    }
}
