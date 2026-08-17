#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleInitializationServiceTests
{
    private sealed class StubMelodyMusicService : IMelodyMusicService
    {
        public void Dispose()
        {
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<MelodyMusic?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubScheduleDisplayNameService : IScheduleDisplayNameService
    {
        public Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule) =>
            Task.CompletedTask;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public async Task InitializeNewScheduleAsync_throws_when_bible_publication_service_is_unavailable()
    {
        var sut = new ScheduleInitializationService(
            TestLogging.CreateLogger(),
            BiblePublicationService: null,
            new StubMelodyMusicService(),
            CreateMapper(),
            new StubScheduleDisplayNameService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InitializeNewScheduleAsync());
    }

    [Fact]
    public async Task LoadExistingScheduleAsync_returns_null_without_database_service()
    {
        var sut = new ScheduleInitializationService(
            TestLogging.CreateLogger(),
            BiblePublicationService: null,
            new StubMelodyMusicService(),
            CreateMapper(),
            new StubScheduleDisplayNameService(),
            alarmScheduleService: null);

        var result = await sut.LoadExistingScheduleAsync(scheduleId: 99, isEnabled: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadExistingScheduleAsync_returns_mapped_schedule_when_database_has_row()
    {
        var entity = new AlarmSchedule
        {
            Id = 77,
            Name = "Evening",
            IsEnabled = false,
            Hour = 21,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Wednesday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };
        var sut = new ScheduleInitializationService(
            TestLogging.CreateLogger(),
            BiblePublicationService: null,
            new StubMelodyMusicService(),
            CreateMapper(),
            new StubScheduleDisplayNameService(),
            alarmScheduleService: new LoadByIdAlarmScheduleStub(entity));

        var result = await sut.LoadExistingScheduleAsync(scheduleId: 77, isEnabled: true);

        Assert.NotNull(result);
        Assert.Equal(77, result!.Id);
        Assert.True(result.IsEnabled);
        Assert.Equal("Evening", result.Name);
    }

    [Fact]
    public async Task LoadExistingScheduleAsync_returns_null_when_schedule_missing()
    {
        var sut = new ScheduleInitializationService(
            TestLogging.CreateLogger(),
            BiblePublicationService: null,
            new StubMelodyMusicService(),
            CreateMapper(),
            new StubScheduleDisplayNameService(),
            alarmScheduleService: new LoadByIdAlarmScheduleStub(null));

        var result = await sut.LoadExistingScheduleAsync(scheduleId: 404, isEnabled: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteScheduleLoadAsync_completes_without_throw()
    {
        var sut = new ScheduleInitializationService(
            TestLogging.CreateLogger(),
            BiblePublicationService: null,
            new StubMelodyMusicService(),
            CreateMapper(),
            new StubScheduleDisplayNameService());

        await sut.CompleteScheduleLoadAsync();
    }

    private sealed class LoadByIdAlarmScheduleStub(AlarmSchedule? schedule) : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(scheduleId == schedule?.Id ? schedule : null);

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

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
}
