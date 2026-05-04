#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackInitializerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class StubPreparePlaybackService : IPreparePlaybackService
    {
        public List<AudioPlayerTrack>? TracksToReturn { get; set; }
        public Func<int, CancellationToken, Task<List<AudioPlayerTrack>?>>? OnPrepareTracks { get; set; }

        public Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default)
        {
            if (OnPrepareTracks != null)
            {
                return OnPrepareTracks(scheduleId, cancellationToken);
            }

            return Task.FromResult(TracksToReturn);
        }

        public Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<AudioPlayerTrack?>(null);
    }

    [Fact]
    public async Task PrepareTracksAsync_dispatches_started_auto_advancing_and_loading_then_returns_tracks()
    {
        var dispatcher = new RecordingDispatcher();
        var prepare = new StubPreparePlaybackService
        {
            TracksToReturn =
            [
                new AudioPlayerTrack { PlayItem = new PlayItem(CreateMeta(), "https://x") }
            ]
        };
        var sut = new PlaybackInitializer(prepare, dispatcher, TestLogging.CreateLogger());

        var result = await sut.PrepareTracksAsync(42, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(3, dispatcher.Dispatched.Count);
        Assert.Equal(42, Assert.IsType<PlaybackStartedAction>(dispatcher.Dispatched[0]).ScheduleId);
        Assert.True(Assert.IsType<SetAutoAdvancingAction>(dispatcher.Dispatched[1]).IsAutoAdvancing);
        Assert.Equal(PlayStatus.Loading, Assert.IsType<PlaybackStatusChangedAction>(dispatcher.Dispatched[2]).Status);
    }

    [Fact]
    public async Task PrepareTracksAsync_returns_null_when_prepare_cancelled()
    {
        var dispatcher = new RecordingDispatcher();
        var prepare = new StubPreparePlaybackService
        {
            OnPrepareTracks = (_, _) => throw new OperationCanceledException()
        };
        var sut = new PlaybackInitializer(prepare, dispatcher, TestLogging.CreateLogger());

        var result = await sut.PrepareTracksAsync(7, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(3, dispatcher.Dispatched.Count);
    }

    private static TrackMetadata CreateMeta() =>
        new()
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "1",
            LookUpPath = "/t"
        };
}
