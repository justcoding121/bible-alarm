#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Tests;

public sealed class OrphanedScheduleCleanupTests
{
    private const string OrphanMusicPublicationCode = "missing-music-pub";
    private const string OrphanMusicTrackCode = "99";

    [Fact]
    public async Task CleanupAsync_returns_immediately_when_database_paths_missing()
    {
        var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());

        await sut.CleanupAsync(
            Path.Combine(Path.GetTempPath(), $"missing-media-{Guid.NewGuid():N}.db"),
            Path.Combine(Path.GetTempPath(), $"missing-sched-{Guid.NewGuid():N}.db"));
    }

    [Fact]
    public async Task CleanupAsync_returns_when_schedule_has_no_qualifying_refs()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = Path.Combine(dir, "empty-schedule.db");
            await using (var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath)))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(0, await CountAlarmSchedulesAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_deletes_schedule_when_bible_track_not_in_media_index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            await AddNotificationAsync(schedulePath);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);

            Assert.Equal(1, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Equal(1, await CountNotificationsAsync(schedulePath));

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(0, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Equal(0, await CountBiblePublicationSchedulesAsync(schedulePath));
            Assert.Equal(0, await CountNotificationsAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_keeps_schedule_when_bible_track_exists_in_media_index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchPublicationWithTrackAsync(mediaPath);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(1, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Equal(1, await CountBiblePublicationSchedulesAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_deletes_flat_bible_schedule_when_track_not_in_media_index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchFlatReferenceAsync(dir);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(0, await CountAlarmSchedulesAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_keeps_flat_bible_schedule_when_track_exists_in_media_index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchFlatReferenceAsync(dir);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchFlatTracksAsync(mediaPath);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(1, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Equal(1, await CountBiblePublicationSchedulesAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_resets_music_when_music_track_missing_and_bible_track_exists()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            await AttachOrphanMusicAsync(schedulePath);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchPublicationWithTrackAsync(mediaPath);

            Assert.Equal(1, await CountAlarmMusicAsync(schedulePath));
            Assert.True(await IsMusicEnabledAsync(schedulePath));

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(1, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Equal(0, await CountAlarmMusicAsync(schedulePath));
            Assert.False(await IsMusicEnabledAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_deletes_schedule_without_separate_music_reset_when_bible_and_music_orphaned()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            await AttachOrphanMusicAsync(schedulePath);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(0, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Equal(0, await CountAlarmMusicAsync(schedulePath));
            Assert.Equal(0, await CountBiblePublicationSchedulesAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_assigns_category_code_when_null_and_publication_exists()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            Assert.Null(await GetCategoryCodeAsync(schedulePath));

            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchDiscoveryAsync(mediaPath);
            await MediaBootstrapTestDatabaseHelper.SeedFrenchPublicationWithTrackAsync(mediaPath);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, await GetCategoryCodeAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task CleanupAsync_leaves_category_null_when_publication_has_no_category_in_media_index()
    {
        var dir = await MediaBootstrapTestDatabaseHelper.CreateTempDirectoryAsync();
        try
        {
            var schedulePath = await MediaBootstrapTestDatabaseHelper.CreateScheduleDbWithFrenchReferenceAsync(dir);
            var mediaPath = await MediaBootstrapTestDatabaseHelper.CreateEmptyMediaIndexDbAsync(dir);
            await MediaBootstrapTestDatabaseHelper.SeedOldFrenchNwtGenesisTrackAsync(mediaPath);

            // Remove category junction so GetFirstCategoryCodeForPubAsync finds nothing.
            await ClearPublicationCategoriesAsync(mediaPath);

            var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());
            await sut.CleanupAsync(mediaPath, schedulePath);

            Assert.Equal(1, await CountAlarmSchedulesAsync(schedulePath));
            Assert.Null(await GetCategoryCodeAsync(schedulePath));
        }
        finally
        {
            MediaBootstrapTestDatabaseHelper.TryDeleteDirectory(dir);
        }
    }

    private static DbContextOptions<ScheduleDbContext> CreateScheduleOptions(string path) =>
        new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite($"Data Source={path}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

    private static DbContextOptions<MediaDbContext> CreateMediaOptions(string path) =>
        new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite($"Data Source={path}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

    private static async Task AttachOrphanMusicAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        var alarm = await db.AlarmSchedules.SingleAsync();
        alarm.MusicEnabled = true;
        db.AlarmMusic.Add(new AlarmMusic
        {
            AlarmScheduleId = alarm.Id,
            AlarmSchedule = alarm,
            PublicationCode = OrphanMusicPublicationCode,
            LanguageCode = MediaBootstrapTestDatabaseHelper.FrenchLanguageCode,
            SectionCode = null,
            TrackCode = OrphanMusicTrackCode,
            Repeat = false,
        });
        await db.SaveChangesAsync();
    }

    private static async Task AddNotificationAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        var alarm = await db.AlarmSchedules.SingleAsync();
        db.AlarmNotifications.Add(new AlarmNotification
        {
            AlarmScheduleId = alarm.Id,
            AlarmSchedule = alarm,
            ScheduledTime = DateTimeOffset.UtcNow.AddHours(1),
            Sent = false,
            Fired = false,
            CancellationRequested = false,
            Cancelled = false,
        });
        await db.SaveChangesAsync();
    }

    private static async Task ClearPublicationCategoriesAsync(string mediaIndexPath)
    {
        await using var db = new MediaDbContext(CreateMediaOptions(mediaIndexPath));
        db.BiblePublicationCategories.RemoveRange(db.BiblePublicationCategories);
        await db.SaveChangesAsync();
    }

    private static async Task<int> CountAlarmSchedulesAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        return await db.AlarmSchedules.CountAsync();
    }

    private static async Task<int> CountBiblePublicationSchedulesAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        return await db.BiblePublicationSchedules.CountAsync();
    }

    private static async Task<int> CountAlarmMusicAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        return await db.AlarmMusic.CountAsync();
    }

    private static async Task<int> CountNotificationsAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        return await db.AlarmNotifications.CountAsync();
    }

    private static async Task<bool> IsMusicEnabledAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        return await db.AlarmSchedules.Select(a => a.MusicEnabled).SingleAsync();
    }

    private static async Task<string?> GetCategoryCodeAsync(string schedulePath)
    {
        await using var db = new ScheduleDbContext(CreateScheduleOptions(schedulePath));
        return await db.AlarmSchedules.Select(a => a.CategoryCode).SingleAsync();
    }
}
