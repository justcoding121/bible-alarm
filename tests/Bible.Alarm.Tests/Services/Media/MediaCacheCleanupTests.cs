#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net.Http;
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

        public Func<TrackMetadata, Task<PlayItem>>? GetNextPlayItemAsyncImpl { get; set; }

        public Func<TrackMetadata, Task<PlayItem>>? GetPreviousPlayItemAsyncImpl { get; set; }

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
            GetNextPlayItemAsyncImpl != null
                ? GetNextPlayItemAsyncImpl(currentTrackMetadata)
                : Task.FromResult(new PlayItem(ItemMeta(currentTrackMetadata.TrackCode), "u"));

        public Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata,
            IFetchProgress? sectionFetchProgress = null) =>
            GetPreviousPlayItemAsyncImpl != null
                ? GetPreviousPlayItemAsyncImpl(currentTrackMetadata)
                : Task.FromResult(new PlayItem(ItemMeta("0"), "u"));

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

        public List<string> DeletedDirectories { get; } = [];

        public string StorageRoot => "stor";
        public string CacheRoot { get; init; } = "cache";

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

        [RequiresAssemblyFiles]
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

        public Task DeleteDirectory(string path)
        {
            DeletedDirectories.Add(path);
            return Task.CompletedTask;
        }

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

    [Fact]
    public async Task GetUnusedCacheFilesAsync_skips_schedule_when_cache_folder_missing()
    {
        var storage = new CleanupStorageStub();
        var playlist = new CleanupPlaylistStub();
        List<AlarmSchedule> schedules = [new() { Id = 50, NumberOfTracksToPlay = 1 }];

        var toDelete = await MediaCacheCleanup.GetUnusedCacheFilesAsync(
            storage,
            playlist,
            id => Path.Combine("cache", id.ToString()),
            lp => $"blob-{lp}",
            schedules,
            CancellationToken.None);

        Assert.Empty(toDelete);
    }

    [Fact]
    public async Task GetUnusedCacheFilesAsync_indefinite_schedule_keeps_neighbor_lookup_paths()
    {
        const int sid = 902;
        var folder = Path.Combine("X:", "cache", sid.ToString());
        static string CacheFilename(string lp) => $"blob-{lp}";

        var playlist = new CleanupPlaylistStub
        {
            NextTracksAsync = _ => Task.FromResult(new List<PlayItem>
            {
                new(PlaylistMeta(sid, "anchor"), "u"),
            }),
            GetNextPlayItemAsyncImpl = _ => Task.FromResult(new PlayItem(PlaylistMeta(sid, "next-neighbor"), "u")),
            GetPreviousPlayItemAsyncImpl = _ => Task.FromResult(new PlayItem(PlaylistMeta(sid, "prev-neighbor"), "u")),
        };

        var storage = new CleanupStorageStub();
        storage.DirectoryExistence[folder] = true;
        storage.FilesUnderDirectory[folder] =
        [
            Path.Combine(folder, CacheFilename("anchor")),
            Path.Combine(folder, CacheFilename("next-neighbor")),
            Path.Combine(folder, CacheFilename("prev-neighbor")),
            Path.Combine(folder, "orphan.dat"),
        ];

        List<AlarmSchedule> schedules = [new() { Id = sid, NumberOfTracksToPlay = 0 }];

        var toDelete = await MediaCacheCleanup.GetUnusedCacheFilesAsync(
            storage,
            playlist,
            id => Path.Combine("X:", "cache", id.ToString()),
            CacheFilename,
            schedules,
            CancellationToken.None);

        var orphaned = Assert.Single(toDelete);
        Assert.Equal(Path.Combine(folder, "orphan.dat"), orphaned);
    }

    [Fact]
    public async Task DeleteScheduleCache_when_schedule_deleted_removes_entire_folder()
    {
        const int sid = 33;
        var folder = Path.Combine("cache", sid.ToString());
        var storage = new CleanupStorageStub();
        storage.DirectoryExistence[folder] = true;
        storage.FilesUnderDirectory[folder] = [Path.Combine(folder, "a.mp3"), Path.Combine(folder, "b.mp3")];

        var alarm = new ScheduleLookupAlarmService();

        await MediaCacheCleanup.DeleteScheduleCacheAsync(
            new DeleteScheduleCacheArgs(
                TestLogging.CreateLogger(),
                storage,
                new CleanupPlaylistStub(),
                alarm,
                new ConcurrentDictionary<string, Task<string?>>(),
                _ => folder,
                lp => lp,
                sid,
                CancellationToken.None));

        Assert.Equal(2, storage.DeletedFiles.Count);
        Assert.Contains(folder, storage.DeletedDirectories);
    }

    [Fact]
    public async Task DeleteScheduleCache_when_playlist_fails_does_not_delete_files()
    {
        const int sid = 34;
        var folder = Path.Combine("cache", sid.ToString());
        var storage = new CleanupStorageStub();
        storage.DirectoryExistence[folder] = true;
        storage.FilesUnderDirectory[folder] = [Path.Combine(folder, "keep.mp3")];

        var alarm = new ScheduleLookupAlarmService();
        alarm.ById[sid] = new AlarmSchedule { Id = sid, NumberOfTracksToPlay = 1 };
        var playlist = new CleanupPlaylistStub
        {
            NextTracksAsync = _ => throw new HttpRequestException("offline"),
        };

        await MediaCacheCleanup.DeleteScheduleCacheAsync(
            new DeleteScheduleCacheArgs(
                TestLogging.CreateLogger(),
                storage,
                playlist,
                alarm,
                new ConcurrentDictionary<string, Task<string?>>(),
                _ => folder,
                lp => $"blob-{lp}",
                sid,
                CancellationToken.None));

        Assert.Empty(storage.DeletedFiles);
    }

    [Fact]
    public async Task DeleteScheduleCache_when_schedule_exists_deletes_unreferenced_files()
    {
        const int sid = 35;
        var folder = Path.Combine("cache", sid.ToString());
        var storage = new CleanupStorageStub();
        storage.DirectoryExistence[folder] = true;
        storage.FilesUnderDirectory[folder] =
        [
            Path.Combine(folder, "blob-keep"),
            Path.Combine(folder, "stale.dat"),
        ];

        var alarm = new ScheduleLookupAlarmService();
        alarm.ById[sid] = new AlarmSchedule { Id = sid, NumberOfTracksToPlay = 1 };
        var playlist = new CleanupPlaylistStub
        {
            NextTracksAsync = _ => Task.FromResult(new List<PlayItem>
            {
                new(PlaylistMeta(sid, "keep"), "u"),
            }),
        };

        await MediaCacheCleanup.DeleteScheduleCacheAsync(
            new DeleteScheduleCacheArgs(
                TestLogging.CreateLogger(),
                storage,
                playlist,
                alarm,
                new ConcurrentDictionary<string, Task<string?>>(),
                _ => folder,
                lp => $"blob-{lp}",
                sid,
                CancellationToken.None));

        var deleted = Assert.Single(storage.DeletedFiles);
        Assert.Equal(Path.Combine(folder, "stale.dat"), deleted);
    }

    [Fact]
    public async Task CleanUpOrphanedFoldersAsync_deletes_non_numeric_and_unknown_schedule_folders()
    {
        var root = Path.Combine(Path.GetTempPath(), "bacache-" + Guid.NewGuid().ToString("N"));
        var validId = 10;
        Directory.CreateDirectory(Path.Combine(root, validId.ToString()));
        Directory.CreateDirectory(Path.Combine(root, "orphan-99"));
        Directory.CreateDirectory(Path.Combine(root, "not-a-number"));

        var storage = new CleanupStorageStub { CacheRoot = root };
        storage.DirectoryExistence[root] = true;
        storage.DirectoryExistence[Path.Combine(root, validId.ToString())] = true;
        storage.FilesUnderDirectory[Path.Combine(root, "orphan-99")] = [Path.Combine(root, "orphan-99", "x.dat")];
        storage.FilesUnderDirectory[Path.Combine(root, "not-a-number")] = [];

        try
        {
            await MediaCacheCleanup.CleanUpOrphanedFoldersAsync(
                TestLogging.CreateLogger(),
                storage,
                root,
                [new AlarmSchedule { Id = validId }],
                paths => MediaCacheCleanup.DeleteFilesAsync(
                    TestLogging.CreateLogger(),
                    storage,
                    new ConcurrentDictionary<string, Task<string?>>(),
                    _ => _,
                    paths));

            Assert.Contains(Path.Combine(root, "orphan-99"), storage.DeletedDirectories);
            Assert.Contains(Path.Combine(root, "not-a-number"), storage.DeletedDirectories);
            Assert.DoesNotContain(Path.Combine(root, validId.ToString()), storage.DeletedDirectories);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class ScheduleLookupAlarmService : IAlarmScheduleService
    {
        public Dictionary<int, AlarmSchedule> ById { get; } = [];

        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.GetValueOrDefault(scheduleId));

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AlarmSchedule>());

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            Expression<Func<AlarmSchedule, bool>>? predicate = null,
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
            Task.FromResult(ById.ContainsKey(scheduleId));

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.Count > 0);

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
