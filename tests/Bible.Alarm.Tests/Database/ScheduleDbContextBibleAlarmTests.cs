#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDbContextBibleAlarmTests
{
    [Fact]
    public void Parameterless_ctor_exposes_db_sets()
    {
        using var db = new ScheduleDbContext();

        Assert.NotNull(db.AlarmSchedules);
        Assert.NotNull(db.GeneralSettings);
    }

    [Fact]
    public async Task Options_ctor_applies_relationships_and_persists_schedule_rows()
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
            var schedule = new AlarmSchedule { Name = "Morning" };
            db.AlarmSchedules.Add(schedule);
            await db.SaveChangesAsync();

            db.BiblePublicationSchedules.Add(new BiblePublicationSchedule
            {
                AlarmScheduleId = schedule.Id,
                PublicationCode = "nwt",
                SectionCode = "gen",
                TrackCode = "1",
            });
            await db.SaveChangesAsync();
        }

        await using var read = new ScheduleDbContext(options);
        Assert.Equal(1, await read.AlarmSchedules.CountAsync());
        Assert.Equal(1, await read.BiblePublicationSchedules.CountAsync());
    }
}

#if DEBUG

[CollectionDefinition("ScheduleDbContextDesignTimeFallbackBibleAlarm", DisableParallelization = true)]
public sealed class ScheduleDbContextDesignTimeFallbackBibleAlarmCollection;

[Collection("ScheduleDbContextDesignTimeFallbackBibleAlarm")]
public sealed class ScheduleDbContextDesignTimeFallbackBibleAlarmTests
{
    [Fact]
    public async Task OnConfiguring_uses_sqlite_file_when_builder_not_preconfigured()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"bible-alarm-scheduledb-{Guid.NewGuid():n}");
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
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }
}

#endif
