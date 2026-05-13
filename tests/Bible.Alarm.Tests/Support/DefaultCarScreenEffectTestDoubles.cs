#nullable enable

using System.Linq;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Scheduler.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests.Support;

internal sealed class StubDefaultScheduleService : IDefaultScheduleService
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

internal sealed class RecordingFluxorDispatcher : IDispatcher
{
    public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
    public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

    public void Dispatch(object action)
    {
        Dispatched.Add(action);
        ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }
}

internal static class DefaultCarScreenDispatchAwait
{
    public static async Task<T> WaitForSingleDispatchAsync<T>(RecordingFluxorDispatcher dispatcher, TimeSpan timeout)
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
}
