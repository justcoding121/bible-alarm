#nullable enable

using System.Linq.Expressions;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class TrackChangeDetectorTests
{
    private sealed class RecordingAlarmScheduleService : IAlarmScheduleService
    {
        public int GetScheduleByIdAsyncCalls { get; private set; }

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
            bool includeBiblePublication = true, CancellationToken cancellationToken = default)
        {
            GetScheduleByIdAsyncCalls++;
            return Task.FromResult<AlarmSchedule?>(null);
        }

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

    [Fact]
    public void SetLastKnownBibleTrack_ignores_non_positive_schedule_id()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        sut.SetLastKnownBibleTrack(0, "gen", "1");
        sut.SetLastKnownBibleTrack(-1, "gen", "1");

        Assert.Equal(0, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_returns_false_when_not_bible_content_without_touching_schedule()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 5,
            IsBibleContent = false,
            TrackCode = "1",
            SectionCode = null,
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(0, alarms.GetScheduleByIdAsyncCalls);
    }
}
