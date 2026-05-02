#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsRemoveScheduleTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
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

    private sealed class IdleScheduleDisplayNameService : IScheduleDisplayNameService
    {
        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule) =>
            Task.CompletedTask;
    }

    private sealed class IdleAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlarmSchedule());

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class IdleAlarmService : IAlarmService
    {
        public Task Create(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Update(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Delete(int scheduleId) => Task.CompletedTask;
    }

    private sealed class IdleMediaCacheService : IMediaCacheService
    {
        public void Dispose()
        {
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) =>
            Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => string.Empty;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => string.Empty;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) =>
            Task.FromResult(false);

        public Task CleanUpAsync() =>
            Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(Bible.Alarm.Shared.Models.Media.PlayItem playItem,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(Bible.Alarm.Shared.Models.Media.PlayItem playItem, int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) =>
            Task.CompletedTask;
    }

    private static IMapper CreateIdleMapper()
    {
        var cfg = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public async Task HandleRemoveSchedule_skips_when_schedule_null()
    {
        var dispatcher = new RecordingDispatcher();

        var sut = new ScheduleEffects(
            CreateIdleMapper(),
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: new IdleAlarmScheduleService(),
                AlarmService: new IdleAlarmService(),
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleRemoveSchedule(new RemoveScheduleAction(null!), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleRemoveSchedule_dispatches_success_with_schedule_id()
    {
        var dispatcher = new RecordingDispatcher();

        var sut = new ScheduleEffects(
            CreateIdleMapper(),
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: new IdleAlarmScheduleService(),
                AlarmService: new IdleAlarmService(),
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleRemoveSchedule(new RemoveScheduleAction(new AlarmSchedule { Id = 55, Name = "x" }), dispatcher);

        var success = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<RemoveScheduleSuccessAction>(success);
        Assert.Equal(55, action.ScheduleId);
    }
}
