#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private DbContextOptions<ScheduleDbContext> Options =>
        new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

    public Task InitializeAsync()
    {
        connection.Open();
        using var bootstrap = new ScheduleDbContext(Options);
        bootstrap.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new AlarmScheduleService(null!));

    [Fact]
    public async Task AddGetUpdateDelete_Roundtrip()
    {
        using var svc = CreateService();
        var added = await svc.AddScheduleAsync(MinimalSchedule("A"));

        Assert.True(added.Id > 0);

        var byId = await svc.GetScheduleByIdAsync(added.Id);
        Assert.NotNull(byId);
        Assert.Equal("A", byId!.Name);

        Assert.True(await svc.ScheduleExistsAsync(added.Id));
        Assert.True(await svc.AnySchedulesExistAsync());

        byId.Name = "B";
        await svc.UpdateScheduleAsync(byId);

        var updated = await svc.GetScheduleByIdAsync(added.Id);
        Assert.Equal("B", updated!.Name);

        await svc.UpdateScheduleByIdAsync(added.Id, s => s.Hour = 7);
        var afterAction = await svc.GetScheduleByIdAsync(added.Id, includeMusic: false, includeBiblePublication: false);
        Assert.Equal(7, afterAction!.Hour);

        await svc.DeleteScheduleAsync(added.Id);
        Assert.False(await svc.ScheduleExistsAsync(added.Id));
        Assert.Null(await svc.GetScheduleByIdAsync(added.Id));
    }

    [Fact]
    public async Task DeleteScheduleAsync_WhenMissing_IsNoOp()
    {
        using var svc = CreateService();
        await svc.DeleteScheduleAsync(999);
        Assert.False(await svc.ScheduleExistsAsync(999));
    }

    [Fact]
    public async Task GetSchedules_WithPredicate_IncludeToggles_Ordering()
    {
        using var svc = CreateService();

        var early = MinimalSchedule("early", lastPlayed: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        early.IsEnabled = false;
        await svc.AddScheduleAsync(early);

        var late = MinimalSchedule("late", lastPlayed: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        late.IsEnabled = true;
        await svc.AddScheduleAsync(late);

        var enabledOnly = await svc.GetSchedulesAsync(s => s.IsEnabled);
        Assert.Single(enabledOnly);
        Assert.Equal("late", enabledOnly[0].Name);

        var all = await svc.GetAllSchedulesAsync(includeMusic: false, includeBiblePublication: false);
        Assert.Equal(2, all.Count);
        Assert.Equal("late", all[0].Name);
        Assert.Equal("early", all[1].Name);

        var withNav = await svc.GetAllSchedulesAsync();
        Assert.NotNull(withNav[0].Music);
        Assert.NotNull(withNav[0].BiblePublicationSchedule);
    }

    [Fact]
    public async Task GetFirstScheduleOrDefault_ReturnsNullWhenEmpty()
    {
        using var svc = CreateService();
        Assert.Null(await svc.GetFirstScheduleOrDefaultAsync());
    }

    [Fact]
    public async Task GetFirstScheduleOrDefault_ReturnsRowWhenPresent()
    {
        using var svc = CreateService();
        await svc.AddScheduleAsync(MinimalSchedule("only"));
        var first = await svc.GetFirstScheduleOrDefaultAsync();
        Assert.NotNull(first);
        Assert.Equal("only", first!.Name);
    }

    [Fact]
    public async Task GetMusicAndBiblePublication_ByScheduleId()
    {
        using var svc = CreateService();
        var schedule = MinimalSchedule("s");
        schedule.Music!.PublicationCode = "melody-pub";
        schedule.BiblePublicationSchedule!.PublicationCode = "bible-pub";

        var saved = await svc.AddScheduleAsync(schedule);

        var music = await svc.GetMusicByScheduleIdAsync(saved.Id);
        Assert.NotNull(music);
        Assert.Equal("melody-pub", music!.PublicationCode);

        var bible = await svc.GetBiblePublicationByScheduleIdAsync(saved.Id);
        Assert.NotNull(bible);
        Assert.Equal("bible-pub", bible!.PublicationCode);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoPendingChanges_ReturnsZero()
    {
        using var svc = CreateService();
        Assert.Equal(0, await svc.SaveChangesAsync());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var svc = CreateService();
        svc.Dispose();
        Assert.Null(Record.Exception(() => svc.Dispose()));
    }

    private static AlarmSchedule MinimalSchedule(string name, DateTime? lastPlayed = null) =>
        new()
        {
            Name = name,
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            CurrentPlayItem = PlayType.Music,
            LatestAlarmNotificationId = 0,
            LastPlayedAtUtc = lastPlayed,
            Music = new AlarmMusic
            {
                PublicationCode = "pub",
                TrackCode = "1",
                Repeat = false
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                TrackCode = "1",
                FinishedDuration = TimeSpan.Zero
            }
        };

    private AlarmScheduleService CreateService() => new(new ScheduleTestScopeFactory(Options));
}
