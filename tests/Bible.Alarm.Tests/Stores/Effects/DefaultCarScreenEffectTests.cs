#nullable enable

using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Effects;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DefaultCarScreenEffectTests
{
    [Fact]
    public async Task HandleSetCarPlayScreen_dispatches_metadata_from_service_on_background_thread()
    {
        var meta = new ScheduleTrackMetadata
        {
            ScheduleId = 42,
            Title = "Morning Psalm",
            Artist = "Speaker",
            Album = "Study",
            ArtworkUrl = "https://example.com/a.png",
        };
        var svc = new StubDefaultScheduleService { CarPlayMeta = meta };
        var sut = new DefaultCarScreenEffect(svc);
        var dispatcher = new RecordingFluxorDispatcher();

        var wait = dispatcher.WaitForDispatchAsync<SetDefaultScheduleMetadataAction>();
        await sut.HandleSetCarPlayScreen(new SetCarPlayScreenAction(), dispatcher);

        var action = await wait;
        Assert.Equal(42, action.ScheduleId);
        Assert.Equal("Morning Psalm", action.Title);
        Assert.Equal("Speaker", action.Artist);
        Assert.Equal("Study", action.Album);
        Assert.Equal("https://example.com/a.png", action.ArtworkUrl);
    }

    [Fact]
    public async Task HandleRotateDefaultSchedule_dispatches_rotation_metadata_from_service()
    {
        var meta = new ScheduleTrackMetadata
        {
            ScheduleId = 99,
            Title = "Rotation Track",
            Artist = "Artist",
            Album = null,
            ArtworkUrl = null,
        };
        var svc = new StubDefaultScheduleService { RotationMeta = meta };
        var sut = new DefaultCarScreenEffect(svc);
        var dispatcher = new RecordingFluxorDispatcher();

        var wait = dispatcher.WaitForDispatchAsync<SetDefaultScheduleMetadataAction>();
        await sut.HandleRotateDefaultSchedule(new RotateDefaultScheduleAction(), dispatcher);

        var action = await wait;
        Assert.Equal(99, action.ScheduleId);
        Assert.Equal("Rotation Track", action.Title);
        Assert.Equal("Artist", action.Artist);
        Assert.Null(action.Album);
        Assert.Null(action.ArtworkUrl);
    }

    [Fact]
    public async Task HandleSetCarPlayScreen_when_service_throws_does_not_dispatch_metadata()
    {
        var svc = new StubDefaultScheduleService
        {
            ThrowOnCarPlay = new InvalidOperationException("car play metadata failed"),
        };
        var sut = new DefaultCarScreenEffect(svc);
        var dispatcher = new RecordingFluxorDispatcher();

        await sut.HandleSetCarPlayScreen(new SetCarPlayScreenAction(), dispatcher);

        await AsyncTestFlush.YieldManyAsync(2048);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is SetDefaultScheduleMetadataAction);
    }

    [Fact]
    public async Task HandleRotateDefaultSchedule_when_service_throws_does_not_dispatch_metadata()
    {
        var svc = new StubDefaultScheduleService
        {
            ThrowOnRotation = new InvalidOperationException("rotation metadata failed"),
        };
        var sut = new DefaultCarScreenEffect(svc);
        var dispatcher = new RecordingFluxorDispatcher();

        await sut.HandleRotateDefaultSchedule(new RotateDefaultScheduleAction(), dispatcher);

        await AsyncTestFlush.YieldManyAsync(2048);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is SetDefaultScheduleMetadataAction);
    }
}
