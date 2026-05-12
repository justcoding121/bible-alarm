#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
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
}
