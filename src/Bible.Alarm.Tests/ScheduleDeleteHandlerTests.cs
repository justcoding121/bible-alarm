#nullable enable

using AutoMapper;
using Bible.Alarm.Shared.Models.Enums;
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
}
