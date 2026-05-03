#nullable enable

using System.IO;
using Bible.Alarm.Shared.Constants;
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

    [Fact]
    public async Task Model_Persists_AlarmNotifications_On_Schedule()
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
        var t1 = new DateTimeOffset(2027, 3, 1, 14, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2027, 3, 1, 14, 5, 0, TimeSpan.Zero);

        await using (var db = new ScheduleDbContext(options))
        {
            var schedule = MinimalSchedule(name: "WithNotifications");

            schedule.AlarmNotifications.Add(new AlarmNotification
            {
                ScheduledTime = t1,
                Sent = false,
                Fired = false,
                CancellationRequested = true,
                Cancelled = false,
                AlarmSchedule = schedule,
            });

            schedule.AlarmNotifications.Add(new AlarmNotification
            {
                ScheduledTime = t2,
                Sent = true,
                Fired = false,
                CancellationRequested = false,
                Cancelled = true,
                AlarmSchedule = schedule,
            });

            db.AlarmSchedules.Add(schedule);
            await db.SaveChangesAsync();

            scheduleId = schedule.Id;
        }

        await using var read = new ScheduleDbContext(options);
        var roundTrip = await read.AlarmSchedules
            .AsNoTracking()
            .Include(a => a.AlarmNotifications)
            .SingleAsync(a => a.Id == scheduleId);

        var ordered = roundTrip.AlarmNotifications.OrderBy(n => n.ScheduledTime).ToList();

        Assert.Equal(2, ordered.Count);
        Assert.Equal(t1, ordered[0].ScheduledTime);
        Assert.False(ordered[0].Sent);
        Assert.False(ordered[0].Cancelled);
        Assert.True(ordered[0].CancellationRequested);

        Assert.Equal(t2, ordered[1].ScheduledTime);
        Assert.True(ordered[1].Sent);
        Assert.True(ordered[1].Cancelled);
        Assert.False(ordered[1].CancellationRequested);
        Assert.False(ordered[1].Fired);
    }

    [Fact]
    public async Task Model_AlarmMusic_Unique_Per_Schedule_Rejects_Duplicate_Fk_From_New_Context()
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
            var schedule = MinimalSchedule(name: "MusicUnique");
            schedule.MusicEnabled = true;

            var music = new AlarmMusic
            {
                PublicationCode = "iam",
                LanguageCode = null,
                SectionCode = "iam-9",
                TrackCode = "1",
                Repeat = false,
                AlarmSchedule = schedule,
            };

            schedule.Music = music;
            db.AlarmSchedules.Add(schedule);
            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using var conflicting = new ScheduleDbContext(options);
        conflicting.AlarmMusic.Add(new AlarmMusic
        {
            PublicationCode = "other",
            LanguageCode = "E",
            SectionCode = null,
            TrackCode = "99",
            Repeat = true,
            AlarmScheduleId = scheduleId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }

    [Fact]
    public async Task Model_BiblePublicationSchedule_Unique_Per_Schedule_Rejects_Duplicate_Fk_From_New_Context()
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
            var schedule = MinimalSchedule(name: "BibleDuplicate");

            var bible = new BiblePublicationSchedule
            {
                PublicationCode = "nw",
                LanguageCode = "E",
                SectionCode = "2",
                TrackCode = "3",
                FinishedDuration = TimeSpan.FromMinutes(12),
                AlarmSchedule = schedule,
            };

            schedule.BiblePublicationSchedule = bible;
            db.AlarmSchedules.Add(schedule);
            await db.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using var conflicting = new ScheduleDbContext(options);
        conflicting.BiblePublicationSchedules.Add(new BiblePublicationSchedule
        {
            PublicationCode = "nw",
            LanguageCode = "M",
            SectionCode = null,
            TrackCode = "400",
            FinishedDuration = TimeSpan.Zero,
            AlarmScheduleId = scheduleId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }

    [Fact]
    public async Task Model_Persists_GeneralSettings_Row()
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

        const string key = "pref.sync-test";
        await using (var db = new ScheduleDbContext(options))
        {
            db.GeneralSettings.Add(new GeneralSettings { Key = key, Value = "active" });
            await db.SaveChangesAsync();
        }

        await using var read = new ScheduleDbContext(options);
        var row = await read.GeneralSettings.AsNoTracking().SingleAsync();

        Assert.Equal(key, row.Key);
        Assert.Equal("active", row.Value);
    }

    [Fact]
    public void Parameterless_ctor_is_supported()
    {
        using var db = new ScheduleDbContext();

        Assert.NotNull(db.AlarmSchedules);
    }
}

#if DEBUG

[CollectionDefinition("ScheduleDbContextDesignTimeFallback", DisableParallelization = true)]
public sealed class ScheduleDbContextDesignTimeFallbackCollection { }

[Collection("ScheduleDbContextDesignTimeFallback")]
public sealed class ScheduleDbContextDesignTimeFallbackTests
{
    [Fact]
    public async Task OnConfiguring_uses_sqlite_file_when_builder_not_preconfigured()
    {
        var temp = Path.Combine(
            Path.GetTempPath(),
            $"bible-alarm-scheduledb-design-{Guid.NewGuid():n}");
        Directory.CreateDirectory(temp);
        var expectedDb = Path.Combine(temp, AppConstants.Database.ScheduleDatabaseFileName);
        var previous = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(temp);

            Assert.False(File.Exists(expectedDb));

            await using var db = new ScheduleDbContext();
            await db.Database.EnsureCreatedAsync();

            Assert.True(File.Exists(expectedDb));
            Assert.Equal(0, await db.AlarmSchedules.CountAsync());

            await db.Database.EnsureDeletedAsync();
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }
}

#endif
