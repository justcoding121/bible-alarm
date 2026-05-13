#nullable enable

using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Effects;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class RotateDefaultScheduleActionTests
{
    [Fact]
    public async Task DefaultCarScreen_effect_maps_rotation_service_metadata_to_SetDefaultScheduleMetadataAction()
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

        await sut.HandleRotateDefaultSchedule(new RotateDefaultScheduleAction(), dispatcher);

        var action = await DefaultCarScreenDispatchAwait.WaitForSingleDispatchAsync<Bible.Alarm.Stores.Actions.Playback.SetDefaultScheduleMetadataAction>(
            dispatcher,
            TimeSpan.FromSeconds(2));
        Assert.Equal(99, action.ScheduleId);
        Assert.Equal("Rotation Track", action.Title);
        Assert.Equal("Artist", action.Artist);
        Assert.Null(action.Album);
        Assert.Null(action.ArtworkUrl);
    }

    [Fact]
    public async Task DefaultCarScreen_effect_when_rotation_service_throws_does_not_dispatch_metadata()
    {
        var svc = new StubDefaultScheduleService
        {
            ThrowOnRotation = new InvalidOperationException("rotation metadata failed"),
        };
        var sut = new DefaultCarScreenEffect(svc);
        var dispatcher = new RecordingFluxorDispatcher();

        await sut.HandleRotateDefaultSchedule(new RotateDefaultScheduleAction(), dispatcher);

        await Task.Delay(300);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is SetDefaultScheduleMetadataAction);
    }
}
