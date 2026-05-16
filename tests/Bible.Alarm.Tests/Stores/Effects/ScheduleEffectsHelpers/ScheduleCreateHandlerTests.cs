#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
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

public sealed class ScheduleCreateHandlerTests
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

    private sealed class RecordingAlarmService : IAlarmService
    {
        public List<AlarmSchedule> Created { get; } = [];

        public Task Create(AlarmSchedule schedule)
        {
            Created.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Update(AlarmSchedule schedule) =>
            throw new NotImplementedException();

        public Task Delete(int scheduleId) =>
            throw new NotImplementedException();
    }

    private sealed class CreateAlarmScheduleStub : IAlarmScheduleService
    {
        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default)
        {
            schedule.Id = 101;
            return Task.FromResult(schedule);
        }

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
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

    [Fact]
    public async Task HandleAsync_skips_when_schedule_null_and_service_unavailable()
    {
        var sut = new ScheduleCreateHandler(CreateMapper(), null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new CreateScheduleAction(null!), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_dispatches_failure_when_alarm_schedule_service_unavailable()
    {
        var sut = new ScheduleCreateHandler(CreateMapper(), null, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();
        var vm = new ScheduleStateItem { Id = 0, Name = "Draft" };

        await sut.HandleAsync(new CreateScheduleAction(vm), dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<CreateScheduleFailureAction>(fail);
        Assert.Same(vm, action.Schedule);
        Assert.Equal("Service unavailable", action.Error);
    }

    [Fact]
    public async Task HandleAsync_persists_dispatches_success_and_creates_alarm_when_enabled()
    {
        var mapper = CreateMapper();
        var vm = mapper.Map<ScheduleStateItem>(new AlarmSchedule
        {
            Id = 0,
            Name = "Morning",
            IsEnabled = true,
            Hour = 8,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        });

        var alarmSvc = new RecordingAlarmService();
        var scheduleSvc = new CreateAlarmScheduleStub();
        var sut = new ScheduleCreateHandler(mapper, scheduleSvc, alarmSvc, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new CreateScheduleAction(vm), dispatcher);

        var success = Assert.Single(dispatcher.Dispatched);
        var ok = Assert.IsType<CreateScheduleSuccessAction>(success);
        Assert.Equal(101, ok.Schedule.Id);

        var scheduled = Assert.Single(alarmSvc.Created);
        Assert.Equal(101, scheduled.Id);
    }

    [Fact]
    public async Task HandleAsync_dispatches_success_without_creating_alarm_when_schedule_disabled()
    {
        var mapper = CreateMapper();
        var vm = mapper.Map<ScheduleStateItem>(new AlarmSchedule
        {
            Id = 0,
            Name = "Off",
            IsEnabled = false,
            Hour = 8,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        });

        var alarmSvc = new RecordingAlarmService();
        var scheduleSvc = new CreateAlarmScheduleStub();
        var sut = new ScheduleCreateHandler(mapper, scheduleSvc, alarmSvc, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new CreateScheduleAction(vm), dispatcher);

        Assert.IsType<CreateScheduleSuccessAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Empty(alarmSvc.Created);
    }

    [Fact]
    public async Task HandleAsync_dispatches_success_when_alarm_service_unavailable_even_if_enabled()
    {
        var mapper = CreateMapper();
        var vm = mapper.Map<ScheduleStateItem>(new AlarmSchedule
        {
            Id = 0,
            Name = "Morning",
            IsEnabled = true,
            Hour = 8,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        });

        var scheduleSvc = new CreateAlarmScheduleStub();
        var sut = new ScheduleCreateHandler(mapper, scheduleSvc, null, new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new CreateScheduleAction(vm), dispatcher);

        var success = Assert.Single(dispatcher.Dispatched);
        var ok = Assert.IsType<CreateScheduleSuccessAction>(success);
        Assert.Equal(101, ok.Schedule.Id);
    }

    [Fact]
    public async Task HandleAsync_dispatches_failure_when_database_add_throws()
    {
        var mapper = CreateMapper();
        var vm = mapper.Map<ScheduleStateItem>(new AlarmSchedule
        {
            Id = 0,
            Name = "Broken",
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        });

        var sut = new ScheduleCreateHandler(
            mapper,
            new ThrowingAddAlarmScheduleStub(),
            new RecordingAlarmService(),
            new RecordingScheduleDisplayNameService());
        var dispatcher = new RecordingDispatcher();

        await sut.HandleAsync(new CreateScheduleAction(vm), dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched);
        var action = Assert.IsType<CreateScheduleFailureAction>(fail);
        Assert.Same(vm, action.Schedule);
        Assert.Equal("db error", action.Error);
    }

    private sealed class ThrowingAddAlarmScheduleStub : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("db error");

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
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
}
