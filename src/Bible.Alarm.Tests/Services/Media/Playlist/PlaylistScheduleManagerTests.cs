#nullable enable

using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using System.Linq.Expressions;

namespace Bible.Alarm.Tests;

public sealed class PlaylistScheduleManagerTests
{
    private sealed class FakeGeneralSettingsService : IGeneralSettingsService
    {
        public GeneralSettings? LastPlayedRow { get; set; }

        public List<(string Key, string Value)> Sets { get; } = [];

        public void Dispose()
        {
        }

        public Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default)
        {
            if (key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId)
            {
                return Task.FromResult(LastPlayedRow);
            }

            return Task.FromResult<GeneralSettings?>(null);
        }

        public Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Sets.Add((key, value));
            return Task.CompletedTask;
        }

        public Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == AppConstants.GeneralSettingsKeys.LastPlayedScheduleId && LastPlayedRow != null);
    }

    private sealed class FakeAlarmScheduleService : IAlarmScheduleService
    {
        public Dictionary<int, AlarmSchedule> ById { get; } = [];

        public AlarmSchedule? First { get; set; }

        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.GetValueOrDefault(scheduleId));

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(First);

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private static AlarmSchedule Sched(int id, string name = "s") =>
        new() { Id = id, Name = name };

    [Fact]
    public async Task GetRelevantScheduleToPlay_prefers_last_played_when_present()
    {
        var settings = new FakeGeneralSettingsService
        {
            LastPlayedRow = new GeneralSettings { Value = "7" },
        };
        var schedules = new FakeAlarmScheduleService();
        schedules.ById[7] = Sched(7);
        schedules.First = Sched(1);

        var sut = new PlaylistScheduleManager(schedules, settings, CancellationToken.None);

        Assert.Equal(7, await sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task GetRelevantScheduleToPlay_falls_back_to_first_when_last_missing()
    {
        var settings = new FakeGeneralSettingsService { LastPlayedRow = null };
        var schedules = new FakeAlarmScheduleService();
        schedules.First = Sched(3);

        var sut = new PlaylistScheduleManager(schedules, settings, CancellationToken.None);

        Assert.Equal(3, await sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task GetRelevantScheduleToPlay_throws_when_no_schedules_exist()
    {
        var settings = new FakeGeneralSettingsService { LastPlayedRow = null };
        var schedules = new FakeAlarmScheduleService { First = null };

        var sut = new PlaylistScheduleManager(schedules, settings, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetRelevantScheduleToPlay());
    }

    [Fact]
    public async Task SaveLastPlayed_persists_schedule_id_setting()
    {
        var settings = new FakeGeneralSettingsService();
        var schedules = new FakeAlarmScheduleService();
        var sut = new PlaylistScheduleManager(schedules, settings, CancellationToken.None);

        await sut.SaveLastPlayed(11);

        var call = Assert.Single(settings.Sets);
        Assert.Equal(AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, call.Key);
        Assert.Equal("11", call.Value);
    }

    [Fact]
    public async Task LoadScheduleForTracks_returns_schedule_when_found()
    {
        var settings = new FakeGeneralSettingsService();
        var schedules = new FakeAlarmScheduleService();
        var expected = Sched(4);
        schedules.ById[4] = expected;
        var sut = new PlaylistScheduleManager(schedules, settings, CancellationToken.None);

        var loaded = await sut.LoadScheduleForTracks(4);

        Assert.Same(expected, loaded);
    }

    [Fact]
    public async Task LoadScheduleForTracks_throws_when_missing()
    {
        var settings = new FakeGeneralSettingsService();
        var schedules = new FakeAlarmScheduleService();
        var sut = new PlaylistScheduleManager(schedules, settings, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.LoadScheduleForTracks(404));
    }

    [Fact]
    public void ValidateScheduleId_rejects_non_positive_ids()
    {
        Assert.Throws<ArgumentException>(() => PlaylistScheduleManager.ValidateScheduleId(0));
    }
}
