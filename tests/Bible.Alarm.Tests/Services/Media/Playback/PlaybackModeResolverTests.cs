#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;
using System.Linq.Expressions;

namespace Bible.Alarm.Tests;

public sealed class PlaybackModeResolverTests
{
    private sealed class StubAlarmScheduleService : IAlarmScheduleService
    {
        public AlarmSchedule? Schedule { get; set; }
        public bool ThrowOnGetById { get; set; }

        public void Dispose()
        {
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetById)
            {
                throw new InvalidOperationException("simulated load failure");
            }

            return Task.FromResult(Schedule);
        }

        public Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null,
            bool includeMusic = true, bool includeBiblePublication = true,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
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

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, true)]
    [InlineData(-3, true)]
    [InlineData(1, false)]
    public async Task IsIndefinitePlaybackAsync_follows_schedule_number_of_tracks(int? numberOfTracksToPlay,
        bool expectedIndefinite)
    {
        var schedules = new StubAlarmScheduleService();
        if (numberOfTracksToPlay.HasValue)
        {
            schedules.Schedule = new AlarmSchedule { NumberOfTracksToPlay = numberOfTracksToPlay.Value };
        }

        var sut = new PlaybackModeResolver(schedules, TestLogging.CreateLogger());

        var result = await sut.IsIndefinitePlaybackAsync(99, CancellationToken.None);

        Assert.Equal(expectedIndefinite, result);
    }

    [Fact]
    public async Task IsIndefinitePlaybackAsync_returns_false_when_schedule_load_throws()
    {
        var schedules = new StubAlarmScheduleService { ThrowOnGetById = true };
        var sut = new PlaybackModeResolver(schedules, TestLogging.CreateLogger());

        Assert.False(await sut.IsIndefinitePlaybackAsync(1, CancellationToken.None));
    }
}
