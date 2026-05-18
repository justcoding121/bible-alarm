#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleUpdateProcessorTests
{
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

    private static IMapper CreateScheduleMapper()
    {
        var cfg = new MapperConfiguration(
            cfg => cfg.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
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

    [Fact]
    public void HandleServiceUnavailable_dispatches_failure_with_message()
    {
        var dispatcher = new RecordingDispatcher();
        var schedule = new ScheduleStateItem { Id = 5, Name = "Morning" };
        var action = new UpdateScheduleFromViewModelAction(schedule);

        ScheduleUpdateProcessor.HandleServiceUnavailable(action, dispatcher);

        var fail = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFailureAction>());
        Assert.Same(schedule, fail.Schedule);
        Assert.Equal("Service unavailable", fail.Error);
    }

    [Fact]
    public void PreserveMusicPropertiesIfNeeded_copies_from_action_when_publication_mismatches()
    {
        var mapped = new ScheduleStateItem
        {
            MusicPublicationCode = "oldpub",
            MusicTrackCode = "1",
            MusicLanguageCode = "E",
            MusicRepeat = false,
            MusicId = 1,
            MusicSectionCode = "s1",
        };
        var actionSchedule = new ScheduleStateItem
        {
            MusicPublicationCode = "newpub",
            MusicTrackCode = "9",
            MusicLanguageCode = "X",
            MusicRepeat = true,
            MusicId = 2,
            MusicSectionCode = "s2",
        };

        ScheduleUpdateProcessor.PreserveMusicPropertiesIfNeeded(mapped, actionSchedule);

        Assert.Equal("newpub", mapped.MusicPublicationCode);
        Assert.Equal("9", mapped.MusicTrackCode);
        Assert.Equal("X", mapped.MusicLanguageCode);
        Assert.True(mapped.MusicRepeat);
        Assert.Equal(2, mapped.MusicId);
        Assert.Equal("s2", mapped.MusicSectionCode);
    }

    [Fact]
    public void PreserveMusicPropertiesIfNeeded_no_op_when_action_publication_empty()
    {
        var mapped = new ScheduleStateItem { MusicPublicationCode = "iam" };
        var actionSchedule = new ScheduleStateItem { MusicPublicationCode = "" };

        ScheduleUpdateProcessor.PreserveMusicPropertiesIfNeeded(mapped, actionSchedule);

        Assert.Equal("iam", mapped.MusicPublicationCode);
    }

    [Fact]
    public void LogScheduleUpdateResult_does_not_throw()
    {
        var saved = new AlarmSchedule { Id = 3, Name = "Test", Music = null };
        ScheduleUpdateProcessor.LogScheduleUpdateResult(saved);
    }

    [Fact]
    public void LogMappingResult_does_not_throw()
    {
        var item = new ScheduleStateItem { MusicPublicationCode = "osg", MusicLanguageCode = "E", MusicTrackCode = "1" };
        ScheduleUpdateProcessor.LogMappingResult(item);
    }

    [Fact]
    public void LogUpdateStart_does_not_throw()
    {
        var schedule = new ScheduleStateItem { Id = 8, Name = "Z" };
        var action = new UpdateScheduleFromViewModelAction(schedule, shouldSave: false);
        ScheduleUpdateProcessor.LogUpdateStart(action);
    }

    [Fact]
    public async Task UpdateAlarmAsync_forwards_saved_schedule_to_alarm_service()
    {
        var alarms = new RecordingAlarmUpdateService();
        var sut = new ScheduleUpdateProcessor(
            CreateScheduleMapper(),
            new IdleAlarmScheduleService(),
            alarms,
            new IdleScheduleDisplayNameService());

        var saved = new AlarmSchedule { Id = 701, Name = "Z" };
        await sut.UpdateAlarmAsync(saved);

        Assert.Same(saved, Assert.Single(alarms.Updated));
    }

    [Fact]
    public async Task MapAndPreserveDisplayNames_copies_music_language_display_from_action_when_codes_align()
    {
        var mapper = CreateScheduleMapper();
        var sut = new ScheduleUpdateProcessor(
            mapper,
            new IdleAlarmScheduleService(),
            new RecordingAlarmUpdateService(),
            new IdleScheduleDisplayNameService());

        var saved = new AlarmSchedule
        {
            Id = 200,
            Name = "Morning",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            Music = new AlarmMusic
            {
                PublicationCode = "iam",
                LanguageCode = "E",
                SectionCode = "1",
                TrackCode = "3",
                Repeat = false,
                AlarmScheduleId = 200,
            },
        };

        var vm = new ScheduleStateItem
        {
            Id = 200,
            MusicPublicationCode = "iam",
            MusicLanguageCode = "E",
            MusicTrackCode = "3",
            MusicLanguageName = "Language display from draft",
            MusicLanguageDirection = "rtl",
        };

        var action = new UpdateScheduleFromViewModelAction(vm);
        var mapped = await sut.MapAndPreserveDisplayNames(action, saved);

        Assert.Equal("Language display from draft", mapped.MusicLanguageName);
        Assert.Equal("rtl", mapped.MusicLanguageDirection);
    }

    [Fact]
    public async Task UpdateScheduleInDatabaseAsync_applies_view_model_changes_via_alarm_service()
    {
        var mapper = CreateScheduleMapper();
        var existing = new AlarmSchedule
        {
            Id = 55,
            Name = "Before",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };
        var scheduleSvc = new UpdatingAlarmScheduleService(existing);
        var sut = new ScheduleUpdateProcessor(
            mapper,
            scheduleSvc,
            new RecordingAlarmUpdateService(),
            new IdleScheduleDisplayNameService());

        var vm = mapper.Map<ScheduleStateItem>(existing);
        vm.Name = "After";
        var action = new UpdateScheduleFromViewModelAction(vm);

        var saved = await sut.UpdateScheduleInDatabaseAsync(action);

        Assert.Equal("After", saved.Name);
        Assert.Equal(55, saved.Id);
        Assert.Equal("After", scheduleSvc.UpdatedEntity?.Name);
    }

    [Fact]
    public void PreserveMusicPropertiesIfNeeded_no_op_when_publication_codes_match()
    {
        var mapped = new ScheduleStateItem { MusicPublicationCode = "iam", MusicTrackCode = "1" };
        var actionSchedule = new ScheduleStateItem { MusicPublicationCode = "iam", MusicTrackCode = "9" };

        ScheduleUpdateProcessor.PreserveMusicPropertiesIfNeeded(mapped, actionSchedule);

        Assert.Equal("iam", mapped.MusicPublicationCode);
        Assert.Equal("1", mapped.MusicTrackCode);
    }

    private sealed class UpdatingAlarmScheduleService : IAlarmScheduleService
    {
        private readonly AlarmSchedule existing;

        public UpdatingAlarmScheduleService(AlarmSchedule existing) => this.existing = existing;

        public AlarmSchedule? UpdatedEntity { get; private set; }

        public void Dispose()
        {
        }

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            updateAction(existing);
            UpdatedEntity = existing;
            return Task.FromResult(existing);
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
}
