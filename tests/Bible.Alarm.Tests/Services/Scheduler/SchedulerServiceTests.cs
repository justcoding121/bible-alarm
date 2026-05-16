#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

[Collection("BootstrapOrchestrator")]
public sealed class SchedulerServiceTests : IDisposable
{
    public SchedulerServiceTests()
    {
        BootstrapHelper.ResetBootstrapStateForTests();
        BootstrapHelper.MarkBootstrapCompleted();
    }

    public void Dispose()
    {
        BootstrapHelper.ResetBootstrapStateForTests();
    }

    private sealed class StubStorageService : IStorageService
    {
        public string StorageRoot => Path.Combine(Path.GetTempPath(), "scheduler-service-tests");

        public string CacheRoot => Path.Combine(StorageRoot, "cache");

        public Task<bool> DirectoryExists(string path) => Task.FromResult(false);

        public Task<bool> FileExists(string path) => Task.FromResult(false);

        public Task<List<string>> GetAllFiles(string path) => Task.FromResult(new List<string>());

        public Task<DateTimeOffset> GetFileCreationDate(string path) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<DateTimeOffset> GetFileCreationDateFromResource(string resourceName) =>
            Task.FromResult(DateTimeOffset.UtcNow);

        public Task<string> ReadFile(string path) => Task.FromResult(string.Empty);

        public Task CopyResourceFile(string resourceFileName, string destinationDirectoryPath, string destinationFileName) =>
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

    private sealed class RecordingMediaCacheService : IMediaCacheService
    {
        public int CleanUpCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => string.Empty;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => string.Empty;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId) => Task.FromResult(false);

        public Task CleanUpAsync()
        {
            CleanUpCalls++;
            return Task.CompletedTask;
        }

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) => Task.CompletedTask;
    }

    private sealed class EnabledScheduleAlarmService : IAlarmScheduleService
    {
        private readonly List<AlarmSchedule> enabled =
        [
            new()
            {
                Id = 3,
                Name = "Enabled",
                IsEnabled = true,
                Hour = 6,
                Minute = 30,
                DaysOfWeek = WeekDays.Monday,
            },
        ];

        public void Dispose()
        {
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(enabled);

        public Task<List<AlarmSchedule>> GetSchedulesAsync(
            System.Linq.Expressions.Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true,
            bool includeBiblePublication = true,
            CancellationToken cancellationToken = default)
        {
            if (predicate == null)
            {
                return Task.FromResult(enabled);
            }

            var compiled = predicate.Compile();
            return Task.FromResult(enabled.Where(compiled).ToList());
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(enabled.FirstOrDefault(s => s.Id == scheduleId));

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(enabled[0]);

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
            Task.FromResult(true);

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmMusic?>(null);

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSchedule?>(null);
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body) =>
            Task.CompletedTask;

        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(false);

        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> CanScheduleAsync() => Task.FromResult(true);
    }

    private sealed class RecordingAlarmService : IAlarmService
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

    [Fact]
    public async Task RescheduleNextOccurrenceAsync_creates_alarm_when_schedule_enabled_and_not_scheduled()
    {
        var alarm = new RecordingAlarmService();
        using var sut = new SchedulerService(
            TestLogging.CreateLogger(),
            new EnabledScheduleAlarmService(),
            new RecordingMediaCacheService(),
            alarm,
            new StubNotificationService(),
            new StubStorageService());

        await sut.RescheduleNextOccurrenceAsync(3);

        Assert.Equal(3, Assert.Single(alarm.Created).Id);
    }
}
