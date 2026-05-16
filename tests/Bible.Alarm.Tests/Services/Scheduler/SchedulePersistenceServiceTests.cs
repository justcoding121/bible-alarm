#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Tests.Support;
using Fluxor;
using System.Linq.Expressions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class SchedulePersistenceServiceTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class SingleScheduleAlarmService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule> { new() { Id = 1, Name = "only" } });

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
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

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
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

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => string.Empty;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => string.Empty;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) => Task.FromResult(false);

        public Task CleanUpAsync() => Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) => Task.CompletedTask;
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task DeleteScheduleAsync_does_not_remove_when_only_one_schedule_exists()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            new IdleAlarmService(),
            dispatcher,
            new IdleMediaCacheService(),
            new SingleScheduleAlarmService());

        await sut.DeleteScheduleAsync(99);

        Assert.Empty(dispatcher.Dispatched);
    }

    private sealed class MultiScheduleAlarmService : IAlarmScheduleService
    {
        private readonly List<AlarmSchedule> schedules =
        [
            new() { Id = 1, Name = "First", IsEnabled = true },
            new() { Id = 2, Name = "Second", IsEnabled = true },
        ];

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(schedules);

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedules);

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(schedules.FirstOrDefault(s => s.Id == scheduleId));

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(schedules[0]);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedules.First(s => s.Id == scheduleId));

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default)
        {
            schedules.RemoveAll(s => s.Id == scheduleId);
            return Task.CompletedTask;
        }

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(schedules.Any(s => s.Id == scheduleId));

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(schedules.Count > 0);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class RecordingAlarmService : IAlarmService
    {
        public int LastDeletedId { get; private set; }

        public Task Create(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Update(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Delete(int scheduleId)
        {
            LastDeletedId = scheduleId;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMediaCacheService : IMediaCacheService
    {
        public int LastDeletedScheduleId { get; private set; }

        public void Dispose()
        {
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => string.Empty;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => string.Empty;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) => Task.FromResult(false);

        public Task CleanUpAsync() => Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId)
        {
            LastDeletedScheduleId = scheduleId;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DeleteScheduleAsync_dispatches_remove_when_multiple_schedules_exist()
    {
        var dispatcher = new RecordingDispatcher();
        var alarm = new RecordingAlarmService();
        var cache = new RecordingMediaCacheService();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            alarm,
            dispatcher,
            cache,
            new MultiScheduleAlarmService());

        await sut.DeleteScheduleAsync(2);

        Assert.Equal(2, alarm.LastDeletedId);
        Assert.Equal(2, cache.LastDeletedScheduleId);
        Assert.IsType<RemoveScheduleAction>(Assert.Single(dispatcher.Dispatched));
    }
}
