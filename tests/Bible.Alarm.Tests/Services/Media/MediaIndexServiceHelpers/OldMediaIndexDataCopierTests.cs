#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class OldMediaIndexDataCopierTests
{
    [Fact]
    public async Task CopyMissingAsync_NoOps_When_ScheduleDbMissing()
    {
        var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
        var tempDir = Path.Combine(Path.GetTempPath(), "bible-alarm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var oldDb = Path.Combine(tempDir, "old.db");
            var newDb = Path.Combine(tempDir, "new.db");
            await File.WriteAllTextAsync(oldDb, "");
            await File.WriteAllTextAsync(newDb, "");

            await sut.CopyMissingAsync(oldDb, newDb, Path.Combine(tempDir, "missing-schedule.db"));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task CopyMissingAsync_NoOps_When_OldMediaIndexMissing()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            var missingOld = Path.Combine(dir, "missing-old.db");

            var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
            await sut.CopyMissingAsync(missingOld, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyMissingAsync_NoOps_When_No_NonEnglishSpanish_References()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = Path.Combine(dir, "english-only-schedule.db");
            var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                .UseSqlite($"Data Source={schedulePath}")
                .Options;
            await using (var db = new ScheduleDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                var alarm = new AlarmSchedule
                {
                    Name = "English only",
                    Hour = 6,
                    Minute = 0,
                    DaysOfWeek = WeekDays.Monday,
                    IsEnabled = true,
                };
                db.AlarmSchedules.Add(alarm);
                await db.SaveChangesAsync();
                db.BiblePublicationSchedules.Add(new BiblePublicationSchedule
                {
                    AlarmScheduleId = alarm.Id,
                    AlarmSchedule = alarm,
                    PublicationCode = MediaBootstrapTestDatabaseHelper.SamplePublicationCode,
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    TrackCode = "1",
                    FinishedDuration = TimeSpan.Zero,
                });
                await db.SaveChangesAsync();
            }

            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");

            var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
            await sut.CopyMissingAsync(oldPath, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyMissingAsync_NoOps_When_Discovery_Filter_Removes_All_References()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");

            var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
            await sut.CopyMissingAsync(oldPath, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyMissingAsync_Copies_Publication_Section_Track_And_Url_From_Old_Index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(newPath);

            var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
            await sut.CopyMissingAsync(oldPath, newPath, schedulePath);

            Assert.Equal(1, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
            Assert.Equal(
                MediaBootstrapTestDatabaseHelper.SampleTrackUrl,
                await MediaBootstrapTestDatabaseHelper.FindTrackUrlAsync(newPath, MediaBootstrapTestDatabaseHelper.SampleTrackCode));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyMissingAsync_Skips_When_Track_Already_Exists_In_New_Index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(newPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchPublicationWithTrackAsync(newPath);

            var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
            await sut.CopyMissingAsync(oldPath, newPath, schedulePath);

            Assert.Equal(1, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }
}
