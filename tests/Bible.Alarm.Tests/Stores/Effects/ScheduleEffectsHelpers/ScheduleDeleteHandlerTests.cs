#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDeleteHandlerTests
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

    private sealed class RecordingScheduleDisplayNameService : IScheduleDisplayNameService
    {
        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule) =>
            Task.CompletedTask;
    }

    private sealed class DeleteAlarmScheduleStub : IAlarmScheduleService
    {
        public required List<AlarmSchedule> AllSchedules { get; init; }

        public AlarmSchedule? ScheduleForGetById { get; init; }

        public List<int> DeletedScheduleIds { get; } = [];

        public Exception? DeleteScheduleException { get; init; }

        public List<string>? DeleteStepTrace { get; init; }

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(AllSchedules);

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(ScheduleForGetById);

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default)
        {
            if (DeleteScheduleException != null)
            {
                throw DeleteScheduleException;
            }

            DeleteStepTrace?.Add($"db:{scheduleId}");
            DeletedScheduleIds.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static AlarmSchedule Alarm(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    [Fact]
    public async Task HandleAsync_dispatches_failure_when_alarm_schedule_service_unavailable()
    {
        var sut = new ScheduleDeleteHandler(CreateMapper(), null, null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new DeleteScheduleAction(12), dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<DeleteScheduleFailureAction>(fail);
        Assert.Equal(12, action.ScheduleId);
        Assert.Equal("Service unavailable", action.Error);
    }

    [Fact]
    public async Task HandleAsync_blocks_delete_when_only_one_schedule_exists()
    {
        var only = Alarm(55, "Solo");
        var svc = new DeleteAlarmScheduleStub
        {
            AllSchedules = [only],
            ScheduleForGetById = only,
        };
        var sut = new ScheduleDeleteHandler(CreateMapper(), svc, null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new DeleteScheduleAction(55), dispatcher);

        Assert.Empty(svc.DeletedScheduleIds);
        var fail = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<DeleteScheduleFailureAction>(fail);
        Assert.Equal("Cannot delete last schedule", action.Error);
    }

    [Fact]
    public async Task HandleAsync_deletes_and_dispatches_success_when_multiple_schedules_exist()
    {
        var a = Alarm(1, "First");
        var b = Alarm(2, "Second");
        var svc = new DeleteAlarmScheduleStub { AllSchedules = [a, b] };
        var sut = new ScheduleDeleteHandler(CreateMapper(), svc, null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new DeleteScheduleAction(2), dispatcher);

        Assert.Equal([2], svc.DeletedScheduleIds);
        var success = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<RemoveScheduleSuccessAction>(success);
        Assert.Equal(2, action.ScheduleId);
    }

    [Fact]
    public async Task HandleAsync_returns_without_dispatch_when_action_is_null()
    {
        var sut = new ScheduleDeleteHandler(CreateMapper(), null, null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(null!, dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_returns_without_dispatch_when_dispatcher_is_null()
    {
        var svc = new DeleteAlarmScheduleStub { AllSchedules = [Alarm(1, "A"), Alarm(2, "B")] };
        var sut = new ScheduleDeleteHandler(CreateMapper(), svc, null, null, new RecordingScheduleDisplayNameService());

        await sut.HandleAsync(new DeleteScheduleAction(2), null!);

        Assert.Empty(svc.DeletedScheduleIds);
    }

    [Fact]
    public async Task HandleAsync_dispatches_failure_when_delete_throws()
    {
        var a = Alarm(1, "First");
        var b = Alarm(2, "Second");
        var svc = new DeleteAlarmScheduleStub
        {
            AllSchedules = [a, b],
            DeleteScheduleException = new InvalidOperationException("db error"),
        };
        var sut = new ScheduleDeleteHandler(CreateMapper(), svc, null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new DeleteScheduleAction(2), dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<DeleteScheduleFailureAction>(fail);
        Assert.Equal(2, action.ScheduleId);
        Assert.Equal("db error", action.Error);
    }

    [Fact]
    public async Task HandleAsync_cleans_cache_and_alarm_before_database_when_optional_services_provided()
    {
        var a = Alarm(1, "First");
        var b = Alarm(2, "Second");
        var ordered = new List<string>();
        var svc = new DeleteAlarmScheduleStub { AllSchedules = [a, b], DeleteStepTrace = ordered };
        var media = new OrderRecordingMediaCacheService(ordered);
        var alarms = new OrderRecordingAlarmService(ordered);
        var sut = new ScheduleDeleteHandler(
            CreateMapper(),
            svc,
            alarms,
            media,
            new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new DeleteScheduleAction(2), dispatcher);

        Assert.Equal(["cache:2", "alarm:2", "db:2"], ordered);
        Assert.Equal([2], svc.DeletedScheduleIds);
        Assert.IsType<RemoveScheduleSuccessAction>(Assert.Single(dispatcher.Dispatched));
    }

    private sealed class OrderRecordingMediaCacheService : IMediaCacheService
    {
        private readonly List<string> order;

        public OrderRecordingMediaCacheService(List<string> order) =>
            this.order = order;

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

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId)
        {
            order.Add($"cache:{scheduleId}");
            return Task.CompletedTask;
        }
    }

    private sealed class OrderRecordingAlarmService : IAlarmService
    {
        private readonly List<string> order;

        public OrderRecordingAlarmService(List<string> order) =>
            this.order = order;

        public Task Create(AlarmSchedule schedule) =>
            Task.CompletedTask;

        public Task Update(AlarmSchedule schedule) =>
            Task.CompletedTask;

        public Task Delete(int scheduleId)
        {
            order.Add($"alarm:{scheduleId}");
            return Task.CompletedTask;
        }
    }
}
