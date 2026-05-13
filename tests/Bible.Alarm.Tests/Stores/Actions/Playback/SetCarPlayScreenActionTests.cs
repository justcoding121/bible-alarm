#nullable enable

using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Effects;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class SetCarPlayScreenActionTests
{
    [Fact]
    public async Task DefaultCarScreen_effect_maps_car_play_service_metadata_to_SetDefaultScheduleMetadataAction()
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

        await sut.HandleSetCarPlayScreen(new SetCarPlayScreenAction(), dispatcher);

        var action = await DefaultCarScreenDispatchAwait.WaitForSingleDispatchAsync<SetDefaultScheduleMetadataAction>(
            dispatcher,
            TimeSpan.FromSeconds(2));
        Assert.Equal(42, action.ScheduleId);
        Assert.Equal("Morning Psalm", action.Title);
        Assert.Equal("Speaker", action.Artist);
        Assert.Equal("Study", action.Album);
        Assert.Equal("https://example.com/a.png", action.ArtworkUrl);
    }

    [Fact]
    public async Task DefaultCarScreen_effect_when_car_play_service_throws_does_not_dispatch_metadata()
    {
        var svc = new StubDefaultScheduleService
        {
            ThrowOnCarPlay = new InvalidOperationException("car play metadata failed"),
        };
        var sut = new DefaultCarScreenEffect(svc);
        var dispatcher = new RecordingFluxorDispatcher();

        await sut.HandleSetCarPlayScreen(new SetCarPlayScreenAction(), dispatcher);

        await Task.Delay(300);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is SetDefaultScheduleMetadataAction);
    }
}
