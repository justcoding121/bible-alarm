#nullable enable

using System.Diagnostics.CodeAnalysis;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheServiceTests
{
    private sealed class StorageStub : IStorageService
    {
        public string StorageRoot => @"Y:\alarm-store";

        public string CacheRoot => "cache-root";

        public Task<bool> DirectoryExists(string path) => Task.FromResult(false);

        public Task<bool> FileExists(string path) => Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        [RequiresAssemblyFiles]
        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult(string.Empty);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath,
            string destinationFileName) =>
            Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, string contents) => Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, byte[] contents) => Task.CompletedTask;

        public Task DeleteFile(string path) => Task.CompletedTask;

        public Task DeleteDirectory(string path) => Task.CompletedTask;

        public Task<DirectoryInfo> CreateDirectory(string path) =>
            Task.FromResult(new DirectoryInfo(path));

        public void Dispose()
        {
        }
    }

    private sealed class IdleDownload : IDownloadService
    {
        public void Dispose()
        {
        }

        public Task<byte[]> DownloadAsync(string url, string? alternativeUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> DownloadWithProgressAsync(string url,
            Action<long, long?>? progressCallback,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(null);

        public Task<bool> FileExists(string url) => Task.FromResult(false);
    }

    private sealed class IdleUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) => Task.FromResult<string?>(null);
    }

    private sealed class IdleNetwork : INetworkStatusService
    {
        public Task<bool> IsInternetAvailable() => Task.FromResult(false);
    }

    private sealed class IdleTrackRefresh : ITrackCdnUrlRefresher
    {
        public Task<string?> TryRefreshTrackCdnUrlFromApiAsync(TrackMetadata metadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class IdlePlaylistService : IPlaylistService
    {
        public void Dispose()
        {
        }

        private static TrackNavigationResult NavDummy() =>
            new("pub", null, new BiblePublicationTrack { TrackCode = "1" });

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(St(), "url"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(null);

        public Task<List<PlayItem>> NextTracks(int scheduleId) =>
            Task.FromResult(new List<PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(NavDummy());

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(NavDummy());

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(St(), "u"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(St(), "u"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata St() =>
            new()
            {
                LookUpPath = "stub-path",
                IsBibleContent = true,
                PublicationCode = "nwt",
                TrackCode = "1",
                ScheduleId = 1,
            };
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
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic,
            bool includeBiblePublication, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic,
            bool includeBiblePublication, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(null);

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(schedule);

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction, CancellationToken cancellationToken = default) =>
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

    private static MediaCacheService CreateSut() =>
        new(new MediaCacheServiceDeps(
            TestLogging.CreateLogger(),
            new StorageStub(),
            new IdleDownload(),
            new IdlePlaylistService(),
            new IdleCatalogMediaService(),
            new IdleNetwork(),
            new IdleUrlRefresh(),
            new IdleAlarmScheduleService(),
            new IdleTrackRefresh()));

    [Fact]
    public async Task SetupAlarmCache_invalid_schedule_returns_false_without_download()
    {
        var sut = CreateSut();
        Assert.False(await sut.SetupAlarmCacheAsync(-1));
        Assert.False(await sut.SetupAlarmCacheAsync(0));
    }

    [Fact]
    public void GetCacheFileName_matches_file_naming_delegate()
    {
        var sut = CreateSut();
        var lp = "pub/track?x";
        Assert.Equal(MediaCacheFileNaming.GetCacheFileName(lp), sut.GetCacheFileName(lp));
    }

    [Fact]
    public void GetCacheFilePath_uses_schedule_subfolder_under_configured_media_cache_root()
    {
        var sut = CreateSut();
        var lp = "lookup/a";
        var path = sut.GetCacheFilePath(lp, 902);
        var fileName = sut.GetCacheFileName(lp);

        Assert.Contains($"{Path.DirectorySeparatorChar}902{Path.DirectorySeparatorChar}", path);
        Assert.Contains($"{Path.DirectorySeparatorChar}MediaCache{Path.DirectorySeparatorChar}", path);
        Assert.True(path.EndsWith(fileName, StringComparison.Ordinal));
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = CreateSut();
        sut.Dispose();
        sut.Dispose();
    }
}
