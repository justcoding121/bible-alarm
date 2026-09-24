#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class OldMediaIndexBackgroundCopierTests
{
    [Fact]
    public async Task CopyRemainingDataAsync_NoOps_When_ScheduleDbMissing()
    {
        var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
        var tempDir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var oldDb = Path.Combine(tempDir, "old.db");
            var newDb = Path.Combine(tempDir, "new.db");
            await File.WriteAllTextAsync(oldDb, "");
            await File.WriteAllTextAsync(newDb, "");

            await sut.CopyRemainingDataAsync(oldDb, newDb, Path.Combine(tempDir, "missing-schedule.db"));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_NoOps_When_OldMediaIndexMissing()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            var missingOld = Path.Combine(dir, "missing-old.db");

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(missingOld, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_NoOps_When_NewMediaIndexMissing()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            var missingNew = Path.Combine(dir, "missing-new.db");

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(oldPath, missingNew, schedulePath);
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_NoOps_When_No_NonEnglishSpanish_References()
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

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(oldPath, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_Skips_When_Publication_Not_In_New_Db()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(newPath);

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(oldPath, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountSectionsAsync(newPath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_Copies_Section_Track_And_Url_When_Publication_Exists()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(newPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchPublicationOnlyAsync(newPath);

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(oldPath, newPath, schedulePath);

            Assert.Equal(1, await MediaBootstrapTestDatabaseHelper.CountSectionsAsync(newPath));
            Assert.Equal(1, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
            Assert.Equal(
                MediaBootstrapTestDatabaseHelper.SampleTrackUrl,
                await MediaBootstrapTestDatabaseHelper.FindTrackUrlAsync(
                    newPath, MediaBootstrapTestDatabaseHelper.SampleTrackCode));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_Skips_Existing_Track_And_Copies_Additional()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(oldPath);
            await MediaBootstrapTestDatabaseHelper.SeedAdditionalOldFrenchGenesisTrackAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(newPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchPublicationWithTrackAsync(newPath);

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(oldPath, newPath, schedulePath);

            Assert.Equal(2, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
            Assert.Equal(
                MediaBootstrapTestDatabaseHelper.SampleExtraTrackUrl,
                await MediaBootstrapTestDatabaseHelper.FindTrackUrlAsync(
                    newPath, MediaBootstrapTestDatabaseHelper.SampleExtraTrackCode));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CopyRemainingDataAsync_Copies_Flat_Tracks_When_Publication_Exists()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchFlatReferenceAsync(dir);
            var oldPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "old.db");
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchFlatTracksAsync(oldPath);
            var newPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir, "new.db");
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(newPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchFlatPublicationOnlyAsync(newPath);

            var sut = new OldMediaIndexBackgroundCopier(TestLogging.CreateLogger());
            await sut.CopyRemainingDataAsync(oldPath, newPath, schedulePath);

            Assert.Equal(0, await MediaBootstrapTestDatabaseHelper.CountSectionsAsync(newPath));
            Assert.Equal(1, await MediaBootstrapTestDatabaseHelper.CountTracksAsync(newPath));
            Assert.Equal(
                MediaBootstrapTestDatabaseHelper.SampleTrackUrl,
                await MediaBootstrapTestDatabaseHelper.FindTrackUrlAsync(
                    newPath, MediaBootstrapTestDatabaseHelper.SampleTrackCode));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }
}
