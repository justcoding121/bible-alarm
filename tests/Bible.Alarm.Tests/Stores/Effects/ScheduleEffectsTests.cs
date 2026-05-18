#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsTests
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

    private sealed class DeleteCapableAlarmScheduleService : IAlarmScheduleService
    {
        public List<AlarmSchedule> AllSchedules { get; init; } = [];
        public List<int> DeletedIds { get; } = [];

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(AllSchedules);

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AllSchedules);

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

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AllSchedules.Count > 0);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class IdleAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
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

    private sealed class RecordingAlarmUpdateService : IAlarmService
    {
        public List<AlarmSchedule> Updated { get; } = [];

        public Task Create(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Update(AlarmSchedule schedule)
        {
            Updated.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Delete(int scheduleId) => Task.CompletedTask;
    }

    private sealed class UpdateCapableAlarmScheduleService : IAlarmScheduleService
    {
        private readonly AlarmSchedule existing;

        public UpdateCapableAlarmScheduleService(AlarmSchedule existing) => this.existing = existing;

        public void Dispose()
        {
        }

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            updateAction(existing);
            return Task.FromResult(existing);
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
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

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId,
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

    private static IMapper CreateScheduleMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
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
    public async Task HandleMusicSectionSelected_returns_without_dispatching_when_current_schedule_missing()
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

        await sut.HandleMusicSectionSelected(new MusicSectionSelectedAction(new MusicStateItem()), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleBiblePublicationCascade_no_op_when_bible_publication_not_flagged_updated()
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

        await sut.HandleBiblePublicationCascade(
            new UpdateScheduleFromViewModelAction(new ScheduleStateItem(), biblePublicationUpdated: false),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleMusicCascade_no_op_when_music_not_flagged_updated()
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

        await sut.HandleMusicCascade(
            new UpdateScheduleFromViewModelAction(new ScheduleStateItem(), musicUpdated: false),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleDeleteSchedule_deletes_from_db_and_dispatches_success()
    {
        var dispatcher = new RecordingDispatcher();
        var alarmSchedules = new DeleteCapableAlarmScheduleService
        {
            AllSchedules = [Alarm(1, "First"), Alarm(2, "Second")],
        };

        var sut = new ScheduleEffects(
            CreateScheduleMapper(),
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: alarmSchedules,
                AlarmService: new IdleAlarmService(),
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleDeleteSchedule(new DeleteScheduleAction(2), dispatcher);

        Assert.Equal([2], alarmSchedules.DeletedIds);
        var success = Assert.IsType<RemoveScheduleSuccessAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Equal(2, success.ScheduleId);
    }

    [Fact]
    public async Task HandleCreateSchedule_persists_and_dispatches_success()
    {
        var dispatcher = new RecordingDispatcher();
        var mapper = CreateScheduleMapper();
        var vm = mapper.Map<ScheduleStateItem>(Alarm(0, "New schedule"));
        vm.Id = 0;

        var alarmSchedules = new CreateCapableAlarmScheduleService();
        var alarmSvc = new RecordingCreateAlarmService();

        var sut = new ScheduleEffects(
            mapper,
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: alarmSchedules,
                AlarmService: alarmSvc,
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleCreateSchedule(new CreateScheduleAction(vm), dispatcher);

        Assert.Equal(201, alarmSchedules.LastAddedId);
        var success = Assert.IsType<CreateScheduleSuccessAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Equal(201, success.Schedule.Id);
        Assert.Equal(201, Assert.Single(alarmSvc.Created).Id);
    }

    [Fact]
    public async Task HandleUpdateScheduleFromViewModel_should_save_false_skips_db_and_success_dispatch()
    {
        var dispatcher = new RecordingDispatcher();
        var mapper = CreateScheduleMapper();
        var vm = mapper.Map<ScheduleStateItem>(Alarm(12, "Draft only"));

        var sut = new ScheduleEffects(
            mapper,
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: new UpdateCapableAlarmScheduleService(Alarm(12, "Draft only")),
                AlarmService: new RecordingAlarmUpdateService(),
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleUpdateScheduleFromViewModel(
            new UpdateScheduleFromViewModelAction(vm, shouldSave: false),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleUpdateScheduleFromViewModel_should_save_true_persists_and_dispatches_success()
    {
        var dispatcher = new RecordingDispatcher();
        var mapper = CreateScheduleMapper();
        var entity = Alarm(33, "Before");
        var vm = mapper.Map<ScheduleStateItem>(entity);
        vm.Name = "After";
        var alarmSchedules = new UpdateCapableAlarmScheduleService(entity);
        var alarmSvc = new RecordingAlarmUpdateService();

        var sut = new ScheduleEffects(
            mapper,
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: alarmSchedules,
                AlarmService: alarmSvc,
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleUpdateScheduleFromViewModel(
            new UpdateScheduleFromViewModelAction(vm, shouldSave: true),
            dispatcher);

        Assert.Equal("After", entity.Name);
        Assert.Equal(33, Assert.Single(alarmSvc.Updated).Id);
        var success = Assert.IsType<UpdateScheduleSuccessAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Equal(33, success.Schedule.Id);
        Assert.Equal("After", success.Schedule.Name);
    }

    [Fact]
    public async Task HandleUpdateScheduleFromViewModel_dispatches_failure_when_display_name_population_throws()
    {
        var dispatcher = new RecordingDispatcher();
        var mapper = CreateScheduleMapper();
        var entity = Alarm(44, "Broken");
        var vm = mapper.Map<ScheduleStateItem>(entity);

        var sut = new ScheduleEffects(
            mapper,
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: new UpdateCapableAlarmScheduleService(entity),
                AlarmService: new RecordingAlarmUpdateService(),
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new ThrowingScheduleDisplayNameService()));

        await sut.HandleUpdateScheduleFromViewModel(
            new UpdateScheduleFromViewModelAction(vm, shouldSave: true),
            dispatcher);

        var fail = Assert.IsType<UpdateScheduleFailureAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Same(vm, fail.Schedule);
        Assert.Equal("display failed", fail.Error);
    }

    private sealed class ThrowingScheduleDisplayNameService : IScheduleDisplayNameService
    {
        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule) =>
            throw new InvalidOperationException("display failed");
    }

    [Fact]
    public async Task HandleUpdateScheduleFromViewModel_skips_when_schedule_null()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new ScheduleEffects(
            CreateScheduleMapper(),
            new ScheduleEffectsOptionalDeps(
                AlarmScheduleService: new UpdateCapableAlarmScheduleService(Alarm(1, "X")),
                AlarmService: new IdleAlarmService(),
                MediaCacheService: new IdleMediaCacheService(),
                State: new FakeApplicationState(new ApplicationState([])),
                ScheduleDisplayNameService: new IdleScheduleDisplayNameService()));

        await sut.HandleUpdateScheduleFromViewModel(
            new UpdateScheduleFromViewModelAction(null!, shouldSave: true),
            dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    private sealed class CreateCapableAlarmScheduleService : IAlarmScheduleService
    {
        public int LastAddedId { get; private set; }

        public void Dispose()
        {
        }

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default)
        {
            schedule.Id = LastAddedId = 201;
            return Task.FromResult(schedule);
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
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

    private sealed class RecordingCreateAlarmService : IAlarmService
    {
        public List<AlarmSchedule> Created { get; } = [];

        public Task Create(AlarmSchedule schedule)
        {
            Created.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Update(AlarmSchedule schedule) => Task.CompletedTask;

        public Task Delete(int scheduleId) => Task.CompletedTask;
    }
}
