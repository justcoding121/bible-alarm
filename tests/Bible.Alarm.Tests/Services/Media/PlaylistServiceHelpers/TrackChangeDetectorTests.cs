#nullable enable

using System.Linq.Expressions;
using System.Reflection;
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

        /// <summary>When non-null, returned by <see cref="GetScheduleByIdAsync"/> instead of always null.</summary>
        public AlarmSchedule? ReturnForGetById { get; set; }

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
            return Task.FromResult(ReturnForGetById);
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

    /// <summary>
    /// Bible schedule loaded from alarm matches supplied metadata track signature (cache primed path).
    /// </summary>
    private sealed class MatchingBibleScheduleAlarmService : IAlarmScheduleService
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
            return Task.FromResult<AlarmSchedule?>(new AlarmSchedule
            {
                Id = scheduleId,
                Name = "Matching bible row",
                BiblePublicationSchedule = new BiblePublicationSchedule
                {
                    AlarmScheduleId = scheduleId,
                    PublicationCode = "nwt",
                    SectionCode = "gen",
                    TrackCode = "1",
                    FinishedDuration = TimeSpan.Zero,
                },
            });
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
    public void BibleTrackSignature_nested_record_can_be_constructed_via_reflection()
    {
        var nested = typeof(TrackChangeDetector).GetNestedTypes(BindingFlags.NonPublic)
            .Single(t => t.Name.Contains("BibleTrackSignature", StringComparison.Ordinal));
        var instance = Activator.CreateInstance(nested, "GEN", "1");

        Assert.NotNull(instance);
        Assert.Equal("GEN", nested.GetProperty("SectionCode")!.GetValue(instance));
        Assert.Equal("1", nested.GetProperty("TrackCode")!.GetValue(instance));
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
    public async Task CheckIfTrackChanged_uses_cached_signature_without_loading_schedule()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        sut.SetLastKnownBibleTrack(7, " gen ", "1");

        var meta = new TrackMetadata
        {
            ScheduleId = 7,
            IsBibleContent = true,
            SectionCode = "GEN",
            TrackCode = "1",
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
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

    [Fact]
    public async Task CheckIfTrackChanged_returns_false_when_schedule_id_non_positive_for_bible_content()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 0,
            IsBibleContent = true,
            TrackCode = "1",
            SectionCode = "gen",
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(0, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_returns_false_when_alarm_schedule_missing_in_database()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 99,
            IsBibleContent = true,
            TrackCode = "1",
            SectionCode = "gen",
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(1, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_repeated_calls_do_not_reload_schedule_when_signature_matches_after_prime()
    {
        var alarms = new MatchingBibleScheduleAlarmService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 101,
            IsBibleContent = true,
            TrackCode = "1",
            SectionCode = "gen",
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(1, alarms.GetScheduleByIdAsyncCalls);

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(1, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_returns_false_when_schedule_row_has_no_bible_publication()
    {
        var alarms = new RecordingAlarmScheduleService
        {
            ReturnForGetById = new AlarmSchedule
            {
                Id = 42,
                Name = "No bible row",
                BiblePublicationSchedule = null,
            },
        };
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 42,
            IsBibleContent = true,
            TrackCode = "1",
            SectionCode = "40",
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(1, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_returns_true_when_cached_signature_differs_from_current()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);
        sut.SetLastKnownBibleTrack(7, "gen", "1");

        var meta = new TrackMetadata
        {
            ScheduleId = 7,
            IsBibleContent = true,
            TrackCode = "2",
            SectionCode = "gen",
            LookUpPath = "path",
        };

        Assert.True(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(0, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_returns_false_for_music_play_type_without_schedule_lookup()
    {
        var alarms = new RecordingAlarmScheduleService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 12,
            IsBibleContent = false,
            TrackCode = "1",
            LookUpPath = "path",
        };

        Assert.False(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(0, alarms.GetScheduleByIdAsyncCalls);
    }

    [Fact]
    public async Task CheckIfTrackChanged_returns_true_when_primed_from_db_differs_from_metadata()
    {
        var alarms = new MatchingBibleScheduleAlarmService();
        var sut = new TrackChangeDetector(alarms, CancellationToken.None);

        var meta = new TrackMetadata
        {
            ScheduleId = 101,
            IsBibleContent = true,
            TrackCode = "9",
            SectionCode = "gen",
            LookUpPath = "path",
        };

        Assert.True(await sut.CheckIfTrackChanged(meta));
        Assert.Equal(1, alarms.GetScheduleByIdAsyncCalls);
    }
}
