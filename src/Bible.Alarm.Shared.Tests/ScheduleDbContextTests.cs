#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class ScheduleDbContextTests
{
    private static AlarmSchedule MinimalSchedule(string name = "Morning") =>
        new()
        {
            Name = name,
            IsEnabled = true,
            Hour = 6,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = false,
            CurrentPlayItem = PlayType.Bible,
            LatestAlarmNotificationId = 0,
            SnoozeMinutes = 7,
            NumberOfTracksToPlay = 0,
            AlwaysPlayFromStart = false,
        };

    [Fact]
    public async Task Model_Persists_BiblePublicationSchedule_OneToOne_WithAlarmSchedule()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new ScheduleDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        int scheduleId;

        await using (var db = new ScheduleDbContext(options))
        {
            var schedule = MinimalSchedule();

            var bible = new BiblePublicationSchedule
            {
                PublicationCode = "nw",
                LanguageCode = "E",
                SectionCode = "1",
                TrackCode = "12",
                FinishedDuration = TimeSpan.FromSeconds(4),
                AlarmSchedule = schedule,
            };

            schedule.BiblePublicationSchedule = bible;
            db.AlarmSchedules.Add(schedule);

            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using (var read = new ScheduleDbContext(options))
        {
            var roundTrip = await read.AlarmSchedules
                .AsNoTracking()
                .Include(a => a.BiblePublicationSchedule)
                .SingleAsync(a => a.Id == scheduleId);

            Assert.NotNull(roundTrip.BiblePublicationSchedule);
            Assert.Equal(scheduleId, roundTrip.BiblePublicationSchedule!.AlarmScheduleId);
            Assert.Equal("nw", roundTrip.BiblePublicationSchedule.PublicationCode);
            Assert.Equal("12", roundTrip.BiblePublicationSchedule.TrackCode);
            Assert.Equal("E", roundTrip.BiblePublicationSchedule.LanguageCode);
            Assert.Equal("1", roundTrip.BiblePublicationSchedule.SectionCode);
        }
    }

    [Fact]
    public async Task Model_Persists_AlarmMusic_OneToOne_WithAlarmSchedule()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new ScheduleDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        int scheduleId;

        await using (var db = new ScheduleDbContext(options))
        {
            var schedule = MinimalSchedule(name: "WithMusic");
            schedule.MusicEnabled = true;

            var music = new AlarmMusic
            {
                PublicationCode = "iam",
                LanguageCode = null,
                SectionCode = "iam-1",
                TrackCode = "10",
                Repeat = false,
                AlarmSchedule = schedule,
            };

            schedule.Music = music;
            db.AlarmSchedules.Add(schedule);

            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using (var read = new ScheduleDbContext(options))
        {
            var rt = await read.AlarmSchedules
                .AsNoTracking()
                .Include(a => a.Music)
                .SingleAsync(a => a.Id == scheduleId);

            Assert.NotNull(rt.Music);
            Assert.Equal("iam", rt.Music!.PublicationCode);
            Assert.Equal("10", rt.Music.TrackCode);
            Assert.Equal(scheduleId, rt.Music.AlarmScheduleId);
        }
    }
}
