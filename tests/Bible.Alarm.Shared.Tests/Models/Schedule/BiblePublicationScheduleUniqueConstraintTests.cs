#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleUniqueConstraintTests
{
    [Fact]
    public async Task SaveChanges_second_bible_publication_schedule_for_same_alarm_fails_one_to_one_constraint()
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

        await using (var db = new ScheduleDbContext(options))
        {
            var schedule = new AlarmSchedule
            {
                Name = "BibleDup",
                IsEnabled = true,
                Hour = 9,
                Minute = 0,
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
            db.AlarmSchedules.Add(schedule);
            await db.SaveChangesAsync();

            db.BiblePublicationSchedules.Add(new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                LanguageCode = "E",
                SectionCode = "1",
                TrackCode = "2",
                FinishedDuration = TimeSpan.FromSeconds(2),
                AlarmScheduleId = schedule.Id,
                AlarmSchedule = schedule,
            });
            await db.SaveChangesAsync();
        }

        var scheduleId = await GetSingleScheduleIdAsync(options);

        await using (var db2 = new ScheduleDbContext(options))
        {
            db2.BiblePublicationSchedules.Add(new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                LanguageCode = "E",
                SectionCode = "3",
                TrackCode = "4",
                FinishedDuration = TimeSpan.FromSeconds(3),
                AlarmScheduleId = scheduleId,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        }
    }

    private static async Task<int> GetSingleScheduleIdAsync(DbContextOptions<ScheduleDbContext> options)
    {
        await using var db = new ScheduleDbContext(options);
        return await db.AlarmSchedules.Select(s => s.Id).SingleAsync();
    }
}
