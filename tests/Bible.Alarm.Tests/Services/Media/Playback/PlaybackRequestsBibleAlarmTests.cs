#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationPreviousRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_previous_navigation_callbacks()
    {
        List<AudioPlayerTrack> playlist = [new() { Uri = "a" }];
        var visited = new HashSet<int>();

        var sut = new PlaybackNavigationPreviousRequest(
            playlist,
            () => 1,
            _ => { },
            5,
            false,
            () => Task.FromResult(false),
            visited,
            _ => Task.CompletedTask,
            _ => Task.FromResult(true),
            () => Task.CompletedTask);

        Assert.Same(playlist, sut.Playlist);
        Assert.Equal(5, sut.CurrentScheduleId);
        Assert.False(sut.IsIndefinitePlayback);
        Assert.Same(visited, sut.ManuallyVisitedTrackIndices);
    }
}

public sealed class PlaybackNavigationNextRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_next_navigation_callbacks()
    {
        var sut = new PlaybackNavigationNextRequest(
            null,
            () => 0,
            _ => { },
            null,
            true,
            () => Task.FromResult(false),
            [],
            _ => Task.CompletedTask,
            _ => Task.FromResult(true),
            () => Task.CompletedTask,
            () => Task.CompletedTask);

        Assert.Null(sut.Playlist);
        Assert.True(sut.IsIndefinitePlayback);
    }
}

public sealed class PlaybackMediaEndedRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_media_ended_handlers()
    {
        var sut = new PlaybackMediaEndedRequest(
            [],
            () => 2,
            _ => { },
            3,
            false,
            () => Task.FromResult(true),
            _ => Task.FromResult(true),
            _ => Task.FromResult(true),
            () => false,
            () => false,
            (_, _) => Task.CompletedTask);

        Assert.Equal(3, sut.CurrentScheduleId);
        Assert.False(sut.GetIsAlarm());
    }
}

public sealed class PlaybackHandleFailureRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_failure_recovery_callbacks()
    {
        List<AudioPlayerTrack> playlist = [];
        var sut = new PlaybackHandleFailureRequest(
            true,
            9,
            () => Task.CompletedTask,
            p => playlist = p,
            _ => { },
            _ => Task.FromResult(true));

        Assert.True(sut.IsAlarm);
        Assert.Equal(9, sut.CurrentScheduleId);
    }
}

public sealed class PlaybackPrepareFallbackRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_prepare_fallback_callbacks()
    {
        var sut = new PlaybackPrepareFallbackRequest(
            12,
            true,
            _ => { },
            _ => { },
            () => { },
            (_, _) => { },
            _ => Task.FromResult(true));

        Assert.Equal(12, sut.ScheduleId);
        Assert.True(sut.KeepErrorMessage);
    }
}

public sealed class PlaybackStopRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_stop_options()
    {
        using var cts = new CancellationTokenSource();
        var resetCalled = false;

        var sut = new PlaybackStopRequest(
            7,
            null,
            true,
            false,
            cts,
            () => resetCalled = true,
            () => { },
            true);

        Assert.Equal(7, sut.ScheduleIdToSave);
        Assert.True(sut.SkipMarkAsPlayed);
        Assert.False(sut.SkipSaveLastPlayed);
        Assert.Same(cts, sut.PreparationCancellationTokenSource);
        Assert.True(sut.SkipDispatchStopped);

        sut.ResetState();
        Assert.True(resetCalled);
    }
}

public sealed class PlaybackMediaFailedRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_media_failed_handlers()
    {
        var sut = new PlaybackMediaFailedRequest(
            [],
            () => 0,
            "uri",
            "url",
            _ => Task.FromResult(true),
            () => false,
            () => true,
            (_, _) => Task.CompletedTask,
            _ => false);

        Assert.Equal("uri", sut.TrackUri);
        Assert.Equal("url", sut.TrackUrl);
        Assert.True(sut.GetIsAlarm());
    }
}

public sealed class PlayTrackRequestBibleAlarmTests
{
    [Fact]
    public void Record_holds_track_playback_state()
    {
        var track = new AudioPlayerTrack { Uri = "u" };
        var played = new HashSet<string>();
        using var cts = new CancellationTokenSource();

        var sut = new PlayTrackRequest(
            track,
            4,
            true,
            8,
            () => false,
            () => null,
            _ => { },
            played,
            cts.Token);

        Assert.Same(track, sut.Track);
        Assert.Equal(4, sut.CurrentTrackIndex);
        Assert.True(sut.StartFromBeginning);
        Assert.Equal(8, sut.CurrentScheduleId);
        Assert.Same(played, sut.PlayedBibleTrackKeys);
        Assert.Equal(cts.Token, sut.CancellationToken);
    }
}
