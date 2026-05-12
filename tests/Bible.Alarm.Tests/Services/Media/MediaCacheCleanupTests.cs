#nullable enable

using System.Collections.Concurrent;
using System.Linq.Expressions;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheCleanupTests
{
    private sealed class CleanupPlaylistStub : IPlaylistService
    {
        public Func<int, Task<List<PlayItem>>> NextTracksAsync { get; set; } =
            _ => Task.FromResult(new List<PlayItem>());

        public void Dispose()
        {
        }

        private static TrackNavigationResult Nav() =>
            new("pub", null, new BiblePublicationTrack { TrackCode = "1" });

        public Task MarkTrackAsPlayed(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<PlayItem> NextTrack(int scheduleId) =>
            Task.FromResult(new PlayItem(ItemMeta(), "url"));

        public Task<PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<PlayItem?>(null);

        public Task<List<PlayItem>> NextTracks(int scheduleId) =>
            NextTracksAsync(scheduleId);

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(Nav());

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
            string publicationCode, string? sectionCode, string trackCode) =>
            Task.FromResult(Nav());

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(
            string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(
            string languageCode, string publicationCode, string sectionCode) =>
            Task.FromResult(default(KeyValuePair<string, BiblePublicationSection>));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(ItemMeta(currentTrackMetadata.TrackCode), "u"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            Task.FromResult(new PlayItem(ItemMeta("0"), "u"));

        public Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata) =>
            Task.CompletedTask;

        private static TrackMetadata ItemMeta(string code = "1") =>
            new()
            {
                ScheduleId = 901,
                IsBibleContent = true,
                PublicationCode = "nwt",
                TrackCode = code,
                LookUpPath = "ignored-for-tests",
            };
    }

    private sealed class CleanupStorageStub : IStorageService
    {
        public Dictionary<string, bool> DirectoryExistence { get; } = [];

        public Dictionary<string, List<string>> FilesUnderDirectory { get; } = [];

        public List<string> DeletedFiles { get; } = [];

        public string StorageRoot => "stor";
        public string CacheRoot => "cache";

        public Task<bool> DirectoryExists(string path) =>
            Task.FromResult(DirectoryExistence.TryGetValue(path, out var ok) && ok);

        public Task<bool> FileExists(string path) => Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path)
        {
            FilesUnderDirectory.TryGetValue(path, out var files);
            return Task.FromResult(files ?? new List<string>());
        }

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult("");

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath,
            string destinationFileName) =>
            Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, string contents) => Task.CompletedTask;

        public Task SaveFile(string directoryPath, string fileName, byte[] contents) => Task.CompletedTask;

        public Task DeleteFile(string path)
        {
            DeletedFiles.Add(path);
            return Task.CompletedTask;
        }

        public Task DeleteDirectory(string path) => Task.CompletedTask;

        public Task<DirectoryInfo> CreateDirectory(string path) =>
            Task.FromResult(new DirectoryInfo(path));

        public void Dispose()
        {
        }
    }

    private sealed class NeverCalledAlarmScheduleService : IAlarmScheduleService
    {
        public void Dispose()
        {
        }

        Task<List<AlarmSchedule>> IAlarmScheduleService.GetAllSchedulesAsync(bool includeMusic,
            bool includeBiblePublication, CancellationToken cancellationToken) =>
            throw Fail();

        Task<List<AlarmSchedule>> IAlarmScheduleService.GetSchedulesAsync(
            Expression<Func<AlarmSchedule, bool>>? predicate,
            bool includeMusic, bool includeBiblePublication,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<AlarmSchedule?> IAlarmScheduleService.GetScheduleByIdAsync(int scheduleId, bool includeMusic,
            bool includeBiblePublication,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<AlarmSchedule?> IAlarmScheduleService.GetFirstScheduleOrDefaultAsync(bool includeMusic,
            bool includeBiblePublication, CancellationToken cancellationToken) =>
            throw Fail();

        Task<AlarmSchedule> IAlarmScheduleService.AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<AlarmSchedule> IAlarmScheduleService.UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<AlarmSchedule> IAlarmScheduleService.UpdateScheduleByIdAsync(int scheduleId,
            Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task IAlarmScheduleService.DeleteScheduleAsync(int scheduleId,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<bool> IAlarmScheduleService.ScheduleExistsAsync(int scheduleId,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<bool> IAlarmScheduleService.AnySchedulesExistAsync(CancellationToken cancellationToken) =>
            throw Fail();

        Task<int> IAlarmScheduleService.SaveChangesAsync(CancellationToken cancellationToken) =>
            throw Fail();

        Task<AlarmMusic?> IAlarmScheduleService.GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken) =>
            throw Fail();

        Task<BiblePublicationSchedule?> IAlarmScheduleService.GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken) =>
            throw Fail();

        private static Exception Fail() =>
            new InvalidOperationException("DeleteSchedule cache tests must not invoke alarm persistence.");
    }

    private static TrackMetadata PlaylistMeta(long scheduleKey, string lookUpPath) =>
        new()
        {
            ScheduleId = scheduleKey,
            IsBibleContent = true,
            PublicationCode = "nwt",
            TrackCode = "1",
            LookUpPath = lookUpPath,
        };

    [Fact]
    public async Task GetUnusedCacheFilesAsync_collects_files_outside_kept_filenames_from_playlist()
    {
        const int sid = 901;
        var folder = Path.Combine("X:", "cache", sid.ToString());
        var playlist = new CleanupPlaylistStub
        {
            NextTracksAsync = _ =>
                Task.FromResult(new List<PlayItem> { new(PlaylistMeta(sid, "pub-a"), "https://one") }),
        };

        static string CacheFilename(string lp) => $"blob-{lp}";

        var storage = new CleanupStorageStub();
        storage.DirectoryExistence[folder] = true;
        storage.FilesUnderDirectory[folder] =
        [
            Path.Combine(folder, CacheFilename("pub-a")),
            Path.Combine(folder, "leftover.tmp"),
        ];

        List<AlarmSchedule> schedules =
        [
            new() { Id = sid, NumberOfTracksToPlay = 99 },
        ];

        string ScheduleFolder(int id) => Path.Combine("X:", "cache", id.ToString());

        var toDelete = await MediaCacheCleanup.GetUnusedCacheFilesAsync(
            storage,
            playlist,
            ScheduleFolder,
            CacheFilename,
            schedules,
            CancellationToken.None);

        var orphaned = Assert.Single(toDelete);
        Assert.Equal(Path.Combine(folder, "leftover.tmp"), orphaned);
    }

    [Fact]
    public void DeleteFiles_skips_matching_inflight_schedule_lookup_pair()
    {
        var storage = new CleanupStorageStub();
        static string CacheFileName(string lookUpPath) => $"blob-{lookUpPath}";

        var inProgress = new ConcurrentDictionary<string, Task<string?>>();
        inProgress["42:chapter1"] = Task.FromResult<string?>(null);

        var busyFile = Path.Combine("C:", "media", "42", CacheFileName("chapter1"));
        var strayFile = Path.Combine("C:", "media", "42", "other.dat");

        MediaCacheCleanup.DeleteFilesAsync(
            TestLogging.CreateLogger(),
            storage,
            inProgress,
            CacheFileName,
            new HashSet<string>(StringComparer.Ordinal) { busyFile, strayFile });

        Assert.Single(storage.DeletedFiles);
        Assert.Equal(strayFile, storage.DeletedFiles[0]);
    }

    [Fact]
    public async Task DeleteScheduleCache_invalid_id_returns_without_calling_alarm_or_storage_helpers()
    {
        var inbox = new ConcurrentDictionary<string, Task<string?>>();
        await MediaCacheCleanup.DeleteScheduleCacheAsync(
            new DeleteScheduleCacheArgs(
                TestLogging.CreateLogger(),
                new CleanupStorageStub(),
                new CleanupPlaylistStub(),
                new NeverCalledAlarmScheduleService(),
                inbox,
                id => $"{id}",
                lp => $"{lp}",
                ScheduleId: 0,
                CancellationToken.None));

        Assert.Empty(inbox);
    }
}
