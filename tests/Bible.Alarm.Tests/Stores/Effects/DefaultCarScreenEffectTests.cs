#nullable enable

using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Effects;
using System.Linq;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class DefaultCarScreenEffectTests
{
    private sealed class StubDefaultScheduleService : IDefaultScheduleService
    {
        public ScheduleTrackMetadata CarPlayMeta { get; init; } = new();
        public ScheduleTrackMetadata RotationMeta { get; init; } = new();

        public Exception? ThrowOnCarPlay { get; init; }

        public Exception? ThrowOnRotation { get; init; }

        public Task<ScheduleTrackMetadata> GetNextScheduleTrackMetaDataAsync() =>
            ThrowOnCarPlay != null
                ? Task.FromException<ScheduleTrackMetadata>(ThrowOnCarPlay)
                : Task.FromResult(CarPlayMeta);

        public Task<ScheduleTrackMetadata> GetNextScheduleInRotationMetadataAsync() =>
            ThrowOnRotation != null
                ? Task.FromException<ScheduleTrackMetadata>(ThrowOnRotation)
                : Task.FromResult(RotationMeta);

        public bool ValidateScheduleIdExists(int scheduleId) => true;

        public int? GetFirstScheduleId() => 1;
    }

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

    private static async Task<T> WaitForSingleDispatchAsync<T>(RecordingDispatcher dispatcher, TimeSpan timeout)
        where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var match = dispatcher.Dispatched.OfType<T>().FirstOrDefault();
            if (match != null)
            {
                return match;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"Expected {typeof(T).Name} to be dispatched within {timeout.TotalMilliseconds} ms.");
        return null!;
    }

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
        var dispatcher = new RecordingDispatcher();

        await sut.HandleSetCarPlayScreen(new SetCarPlayScreenAction(), dispatcher);

        var action = await WaitForSingleDispatchAsync<SetDefaultScheduleMetadataAction>(dispatcher, TimeSpan.FromSeconds(2));
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
        var dispatcher = new RecordingDispatcher();

        await sut.HandleRotateDefaultSchedule(new RotateDefaultScheduleAction(), dispatcher);

        var action = await WaitForSingleDispatchAsync<SetDefaultScheduleMetadataAction>(dispatcher, TimeSpan.FromSeconds(2));
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
        var dispatcher = new RecordingDispatcher();

        await sut.HandleSetCarPlayScreen(new SetCarPlayScreenAction(), dispatcher);

        await Task.Delay(300);
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
        var dispatcher = new RecordingDispatcher();

        await sut.HandleRotateDefaultSchedule(new RotateDefaultScheduleAction(), dispatcher);

        await Task.Delay(300);
        Assert.DoesNotContain(dispatcher.Dispatched, a => a is SetDefaultScheduleMetadataAction);
    }
}
