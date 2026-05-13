#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmMusicUniqueConstraintTests
{
    [Fact]
    public async Task SaveChanges_second_alarm_music_for_same_schedule_fails_one_to_one_constraint()
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
                Name = "MusicDup",
                IsEnabled = true,
                Hour = 8,
                Minute = 0,
                Second = 0,
                DaysOfWeek = WeekDays.Monday,
                NotificationEnabled = true,
                MusicEnabled = true,
                CurrentPlayItem = PlayType.Music,
                LatestAlarmNotificationId = 0,
                SnoozeMinutes = 7,
                NumberOfTracksToPlay = 0,
                AlwaysPlayFromStart = false,
            };
            db.AlarmSchedules.Add(schedule);
            await db.SaveChangesAsync();

            db.AlarmMusic.Add(new AlarmMusic
            {
                PublicationCode = "iam",
                TrackCode = "1",
                Repeat = true,
                AlarmScheduleId = schedule.Id,
                AlarmSchedule = schedule,
            });
            await db.SaveChangesAsync();
        }

        var scheduleId = await GetSingleScheduleIdAsync(options);

        await using (var db2 = new ScheduleDbContext(options))
        {
            db2.AlarmMusic.Add(new AlarmMusic
            {
                PublicationCode = "osg",
                TrackCode = "2",
                Repeat = false,
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
