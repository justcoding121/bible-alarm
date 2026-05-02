#nullable enable

using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Tests.Support;
using System.Linq.Expressions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleUpdaterTests
{
    private sealed class StubAlarmScheduleService : IAlarmScheduleService
    {
        public AlarmSchedule Schedule { get; set; } = null!;

        public void Dispose()
        {
        }

        public Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction,
            CancellationToken cancellationToken = default)
        {
            updateAction(Schedule);
            return Task.FromResult(Schedule);
        }

        public Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true,
            bool includeBiblePublication = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmSchedule?>(Schedule);

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

    private static AlarmSchedule ScheduleWithBible(int id = 1) =>
        new()
        {
            Id = id,
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                SectionCode = "1",
                TrackCode = "1",
                FinishedDuration = TimeSpan.FromMinutes(1),
            },
        };

    private static TrackNavigationResult Nav(string publicationCode, string? sectionCode, string trackCode) =>
        new(publicationCode,
            sectionCode == null ? null : new BiblePublicationSection { SectionCode = sectionCode },
            new BiblePublicationTrack { TrackCode = trackCode, Title = "T" });

    [Fact]
    public async Task UpdateScheduleToNextTrackAsync_updates_bible_publication_fields()
    {
        var schedule = ScheduleWithBible();
        var schedules = new StubAlarmScheduleService { Schedule = schedule };
        var sut = new ScheduleUpdater(schedules, CancellationToken.None);
        var next = Nav("jwpub", "40", "12");

        await sut.UpdateScheduleToNextTrackAsync(schedule.Id, next);

        var brs = schedule.BiblePublicationSchedule!;
        Assert.Equal("jwpub", brs.PublicationCode);
        Assert.Equal("40", brs.SectionCode);
        Assert.Equal("12", brs.TrackCode);
        Assert.Equal(TimeSpan.Zero, brs.FinishedDuration);
    }

    [Fact]
    public async Task UpdateScheduleToPreviousTrackAsync_clears_section_when_previous_has_none()
    {
        var schedule = ScheduleWithBible();
        var schedules = new StubAlarmScheduleService { Schedule = schedule };
        var sut = new ScheduleUpdater(schedules, CancellationToken.None);
        var previous = Nav("nwt", null, "5");

        await sut.UpdateScheduleToPreviousTrackAsync(schedule.Id, previous);

        var brs = schedule.BiblePublicationSchedule!;
        Assert.Null(brs.SectionCode);
        Assert.Equal("5", brs.TrackCode);
    }

    [Fact]
    public async Task GetScheduleWithBiblePublicationAsync_returns_schedule_when_bible_present()
    {
        var schedule = ScheduleWithBible(9);
        var schedules = new StubAlarmScheduleService { Schedule = schedule };
        var sut = new ScheduleUpdater(schedules, CancellationToken.None);

        var result = await sut.GetScheduleWithBiblePublicationAsync(9);

        Assert.Same(schedule, result);
    }

    [Fact]
    public async Task GetScheduleWithBiblePublicationAsync_throws_when_bible_missing()
    {
        var schedule = new AlarmSchedule { Id = 3, BiblePublicationSchedule = null };
        var schedules = new StubAlarmScheduleService { Schedule = schedule };
        var sut = new ScheduleUpdater(schedules, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetScheduleWithBiblePublicationAsync(3));
    }

    [Fact]
    public async Task UpdateScheduleToNextTrackAsync_throws_when_bible_publication_schedule_missing()
    {
        var schedule = new AlarmSchedule { Id = 7, BiblePublicationSchedule = null };
        var schedules = new StubAlarmScheduleService { Schedule = schedule };
        var sut = new ScheduleUpdater(schedules, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateScheduleToNextTrackAsync(7, Nav("pub", null, "1")));
    }

    [Fact]
    public async Task UpdateScheduleToPreviousTrackAsync_throws_when_bible_publication_schedule_missing()
    {
        var schedule = new AlarmSchedule { Id = 8, BiblePublicationSchedule = null };
        var schedules = new StubAlarmScheduleService { Schedule = schedule };
        var sut = new ScheduleUpdater(schedules, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateScheduleToPreviousTrackAsync(8, Nav("pub", null, "2")));
    }
}
