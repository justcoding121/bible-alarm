#nullable enable

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
    private readonly object _gate = new();
    private readonly List<object> _dispatched = [];
    private readonly List<(Type Type, TaskCompletionSource<object> Tcs)> _pending = [];

#pragma warning disable CS0067
    public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

    /// <summary>
    /// Snapshot copy for assertions (thread-safe).
    /// </summary>
    public List<object> Dispatched
    {
        get
        {
            lock (_gate)
            {
                return _dispatched.ToList();
            }
        }
    }

    public Task<T> WaitForDispatchAsync<T>()
        where T : class
    {
        TaskCompletionSource<object>? tcs;
        lock (_gate)
        {
            foreach (var item in _dispatched)
            {
                if (item is T match)
                {
                    return Task.FromResult(match);
                }
            }

            tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add((typeof(T), tcs));
        }

        return AwaitTyped(tcs);

        static async Task<T> AwaitTyped(TaskCompletionSource<object> source)
        {
            return (T)(await source.Task.ConfigureAwait(false));
        }
    }

    public void Dispatch(object action)
    {
        lock (_gate)
        {
            _dispatched.Add(action);

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var (type, pendingTcs) = _pending[i];
                if (!type.IsInstanceOfType(action))
                {
                    continue;
                }

                _pending.RemoveAt(i);
                pendingTcs.TrySetResult(action);
            }
        }

        ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }
}

internal static class AsyncTestFlush
{
    /// <summary>
    /// Lets thread-pool continuations from <see cref="M:System.Threading.Tasks.Task.Run(System.Action)"/> run without using <see cref="M:System.Threading.Tasks.Task.Delay(System.Int32)"/>.
    /// </summary>
    public static async Task YieldManyAsync(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            await Task.Yield();
        }
    }
}
