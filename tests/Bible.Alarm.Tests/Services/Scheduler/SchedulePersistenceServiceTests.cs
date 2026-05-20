#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
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

    private sealed class AssigningIdAlarmScheduleService : IAlarmScheduleService
    {
        private int nextId = 100;

        public AlarmSchedule? LastAdded { get; private set; }

        public AlarmSchedule? LastUpdated { get; private set; }

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

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
            CancellationToken cancellationToken = default)
        {
            schedule.Id = nextId++;
            LastAdded = schedule;
            return Task.FromResult(schedule);
        }

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            var existing = new AlarmSchedule { Id = scheduleId, Name = "Before", Hour = 6, Minute = 0 };
            updateAction(existing);
            LastUpdated = existing;
            return Task.FromResult(existing);
        }

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

        public List<AlarmSchedule> Updated { get; } = [];

        public Task Create(AlarmSchedule schedule)
        {
            Created.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Update(AlarmSchedule schedule)
        {
            Updated.Add(schedule);
            return Task.CompletedTask;
        }

        public Task Delete(int scheduleId) => Task.CompletedTask;
    }

    private sealed class ThrowingAddAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

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
            throw new InvalidOperationException("db unavailable");

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

    [Fact]
    public async Task SaveScheduleAsync_new_enabled_schedule_creates_alarm_and_dispatches_add()
    {
        var dispatcher = new RecordingDispatcher();
        var alarm = new RecordingCreateAlarmService();
        var schedules = new AssigningIdAlarmScheduleService();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            alarm,
            dispatcher,
            new IdleMediaCacheService(),
            schedules);

        var schedule = new AlarmSchedule { Name = "Morning", IsEnabled = true, Hour = 7, Minute = 30 };

        var saved = await sut.SaveScheduleAsync(schedule, isNewSchedule: true);

        Assert.True(saved);
        Assert.Equal(100, schedules.LastAdded!.Id);
        Assert.Equal(schedules.LastAdded, Assert.Single(alarm.Created));
        Assert.IsType<AddScheduleAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task SaveScheduleAsync_existing_schedule_dispatches_update()
    {
        var dispatcher = new RecordingDispatcher();
        var alarm = new RecordingCreateAlarmService();
        var schedules = new AssigningIdAlarmScheduleService();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            alarm,
            dispatcher,
            new IdleMediaCacheService(),
            schedules);

        var schedule = new AlarmSchedule
        {
            Id = 12,
            Name = "Evening",
            Hour = 20,
            Minute = 15,
            DaysOfWeek = WeekDays.Friday,
        };

        var saved = await sut.SaveScheduleAsync(schedule, isNewSchedule: false);

        Assert.True(saved);
        Assert.Equal(12, schedules.LastUpdated!.Id);
        Assert.Equal("Evening", schedules.LastUpdated.Name);
        Assert.Equal(schedules.LastUpdated, Assert.Single(alarm.Updated));
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task SaveScheduleAsync_returns_false_when_database_add_fails()
    {
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            new RecordingCreateAlarmService(),
            new RecordingDispatcher(),
            new IdleMediaCacheService(),
            new ThrowingAddAlarmScheduleService());

        var saved = await sut.SaveScheduleAsync(new AlarmSchedule { Name = "Fail" }, isNewSchedule: true);

        Assert.False(saved);
    }

    [Fact]
    public async Task SaveScheduleAsync_new_disabled_schedule_does_not_create_platform_alarm()
    {
        var dispatcher = new RecordingDispatcher();
        var alarm = new RecordingCreateAlarmService();
        var schedules = new AssigningIdAlarmScheduleService();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            alarm,
            dispatcher,
            new IdleMediaCacheService(),
            schedules);

        var schedule = new AlarmSchedule { Name = "Disabled", IsEnabled = false };

        var saved = await sut.SaveScheduleAsync(schedule, isNewSchedule: true);

        Assert.True(saved);
        Assert.Empty(alarm.Created);
        Assert.IsType<AddScheduleAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task DeleteScheduleAsync_returns_early_when_schedule_not_found()
    {
        var dispatcher = new RecordingDispatcher();
        var alarm = new RecordingAlarmService();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            alarm,
            dispatcher,
            new IdleMediaCacheService(),
            new MultiScheduleAlarmService());

        await sut.DeleteScheduleAsync(999);

        Assert.Equal(0, alarm.LastDeletedId);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task DeleteScheduleAsync_swallows_exceptions_from_dependencies()
    {
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            new IdleAlarmService(),
            new RecordingDispatcher(),
            new IdleMediaCacheService(),
            new ThrowingGetAllAlarmScheduleService());

        await sut.DeleteScheduleAsync(1);
    }

    [Fact]
    public async Task SaveScheduleAsync_update_applies_music_and_bible_when_flags_set()
    {
        var dispatcher = new RecordingDispatcher();
        var alarm = new RecordingCreateAlarmService();
        var schedules = new MusicAndBibleAssigningIdAlarmScheduleService();
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            alarm,
            dispatcher,
            new IdleMediaCacheService(),
            schedules);

        var schedule = new AlarmSchedule
        {
            Id = 5,
            Name = "Updated",
            Hour = 8,
            Minute = 45,
            Music = new AlarmMusic
            {
                PublicationCode = "new-pub",
                TrackCode = "9",
                LanguageCode = "E",
                SectionCode = "40",
                Repeat = true,
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                SectionCode = "41",
                TrackCode = "2",
                LanguageCode = "E",
            },
        };

        var saved = await sut.SaveScheduleAsync(schedule, isNewSchedule: false, musicUpdated: true, biblePublicationUpdated: true);

        Assert.True(saved);
        Assert.NotNull(schedules.LastUpdated);
        Assert.Equal("new-pub", schedules.LastUpdated!.Music!.PublicationCode);
        Assert.True(schedules.LastUpdated.Music.Repeat);
        Assert.Equal(TimeSpan.Zero, schedules.LastUpdated.BiblePublicationSchedule!.FinishedDuration);
        Assert.Equal("41", schedules.LastUpdated.BiblePublicationSchedule.SectionCode);
        Assert.IsType<UpdateScheduleAction>(Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public void Dispose_cancels_and_disposes_token_on_first_call()
    {
        var sut = new SchedulePersistenceService(
            TestLogging.CreateLogger(),
            new IdleAlarmService(),
            new RecordingDispatcher(),
            new IdleMediaCacheService(),
            new SingleScheduleAlarmService());

        sut.Dispose();
    }

    [Fact]
    public void Dispose_is_idempotent_and_logs_when_cancellation_token_disposal_fails()
    {
        var events = new List<Serilog.Events.LogEvent>();
        var logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new LogListSink(events))
            .CreateLogger();

        var sut = new SchedulePersistenceService(
            logger,
            new IdleAlarmService(),
            new RecordingDispatcher(),
            new IdleMediaCacheService(),
            new SingleScheduleAlarmService());

        var field = typeof(SchedulePersistenceService).GetField(
            "cancellationTokenSource",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        ((CancellationTokenSource)field!.GetValue(sut)!).Dispose();

        sut.Dispose();
        sut.Dispose();

        Assert.Contains(events, log =>
            log.Level == Serilog.Events.LogEventLevel.Warning
            && log.MessageTemplate.Text.Contains("cancellation", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ThrowingGetAllAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("db down");

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

    private sealed class MusicAndBibleAssigningIdAlarmScheduleService : IAlarmScheduleService
    {
        public AlarmSchedule? LastUpdated { get; private set; }

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

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
            CancellationToken cancellationToken = default)
        {
            var existing = new AlarmSchedule
            {
                Id = scheduleId,
                Name = "Before",
                Music = new AlarmMusic { PublicationCode = "old", TrackCode = "1", Repeat = false },
                BiblePublicationSchedule = new BiblePublicationSchedule
                {
                    PublicationCode = "nwt",
                    SectionCode = "1",
                    TrackCode = "1",
                    FinishedDuration = TimeSpan.FromMinutes(3),
                },
            };
            updateAction(existing);
            LastUpdated = existing;
            return Task.FromResult(existing);
        }

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

    private sealed class LogListSink(List<Serilog.Events.LogEvent> events) : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) => events.Add(logEvent);
    }
}
