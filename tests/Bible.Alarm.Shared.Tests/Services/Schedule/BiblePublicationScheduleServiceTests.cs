#nullable enable

using System.Reflection;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bible.Alarm.Shared.Tests;

/// <summary>
/// Integration tests for <see cref="BiblePublicationScheduleService"/> backed by SQLite in memory.
/// </summary>
public sealed class BiblePublicationScheduleServiceTests : IAsyncLifetime
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

    private sealed class ListSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new BiblePublicationScheduleService(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new BiblePublicationScheduleService(new ScheduleTestScopeFactory(Options), null!));

    [Fact]
    public async Task GetBiblePublicationScheduleByScheduleIdAsync_ReturnsNull_When_No_Row()
    {
        using var bibleSvc = new BiblePublicationScheduleService(
            new ScheduleTestScopeFactory(Options),
            TestLogging.CreateLogger());

        Assert.Null(await bibleSvc.GetBiblePublicationScheduleByScheduleIdAsync(4242));
    }

    [Fact]
    public async Task GetByScheduleId_GetAll_GetById_Predicate_Update_Exists_Delete_roundtrip()
    {
        var factory = new ScheduleTestScopeFactory(Options);
        using var alarmSvc = new AlarmScheduleService(factory);
        using var bibleSvc = new BiblePublicationScheduleService(factory, TestLogging.CreateLogger());

        var schedule = MinimalSchedule("with-bible");
        schedule.BiblePublicationSchedule!.LanguageCode = "E";

        var saved = await alarmSvc.AddScheduleAsync(schedule);
        var bibleId = saved.BiblePublicationSchedule!.Id;
        Assert.True(bibleId > 0);

        var bySchedule = await bibleSvc.GetBiblePublicationScheduleByScheduleIdAsync(saved.Id);
        Assert.NotNull(bySchedule);
        Assert.Equal(bibleId, bySchedule!.Id);
        Assert.Equal("E", bySchedule.LanguageCode);

        var all = await bibleSvc.GetAllBiblePublicationSchedulesAsync();
        Assert.Single(all);
        Assert.Equal(bibleId, all[0].Id);

        var byId = await bibleSvc.GetBiblePublicationScheduleByIdAsync(bibleId);
        Assert.NotNull(byId);
        Assert.Equal("nwt", byId!.PublicationCode);

        var filtered = await bibleSvc.GetBiblePublicationSchedulesAsync(b => b.LanguageCode == "E");
        Assert.Single(filtered);

        Assert.True(await bibleSvc.BiblePublicationScheduleExistsAsync(bibleId));

        byId.LanguageCode = "M";
        byId.SectionCode = "40";
        var updated = await bibleSvc.UpdateBiblePublicationScheduleAsync(byId);
        Assert.Equal("M", updated.LanguageCode);
        Assert.Equal("40", updated.SectionCode);

        var reloaded = await bibleSvc.GetBiblePublicationScheduleByIdAsync(bibleId);
        Assert.Equal("M", reloaded!.LanguageCode);

        await bibleSvc.DeleteBiblePublicationScheduleAsync(bibleId);
        Assert.Null(await bibleSvc.GetBiblePublicationScheduleByIdAsync(bibleId));
        Assert.False(await bibleSvc.BiblePublicationScheduleExistsAsync(bibleId));
    }

    [Fact]
    public async Task DeleteBiblePublicationScheduleAsync_WhenMissing_IsNoOp()
    {
        using var bibleSvc = new BiblePublicationScheduleService(new ScheduleTestScopeFactory(Options), TestLogging.CreateLogger());

        await bibleSvc.DeleteBiblePublicationScheduleAsync(999_999);
        Assert.False(await bibleSvc.BiblePublicationScheduleExistsAsync(999_999));
    }

    [Fact]
    public async Task AddBiblePublicationScheduleAsync_persists_linked_to_alarm_when_none_on_insert()
    {
        var factory = new ScheduleTestScopeFactory(Options);
        using var alarmSvc = new AlarmScheduleService(factory);
        using var bibleSvc = new BiblePublicationScheduleService(factory, TestLogging.CreateLogger());

        var saved = await alarmSvc.AddScheduleAsync(MinimalScheduleWithoutBible("add-bible-later"));
        var entity = new BiblePublicationSchedule
        {
            AlarmScheduleId = saved.Id,
            PublicationCode = "nwt",
            TrackCode = "2",
            FinishedDuration = TimeSpan.FromMinutes(1),
            LanguageCode = "E",
            SectionCode = "40",
        };

        var inserted = await bibleSvc.AddBiblePublicationScheduleAsync(entity);

        Assert.True(inserted.Id > 0);
        Assert.Equal(saved.Id, inserted.AlarmScheduleId);

        var fetched = await bibleSvc.GetBiblePublicationScheduleByScheduleIdAsync(saved.Id);
        Assert.NotNull(fetched);
        Assert.Equal("40", fetched!.SectionCode);
    }

    [Fact]
    public void Dispose_LogsWarning_When_CancellationTokenCleanupThrows()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var svc = new BiblePublicationScheduleService(new ScheduleTestScopeFactory(Options), logger);

        var field = typeof(BiblePublicationScheduleService).GetField(
            "cancellationTokenSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        ((CancellationTokenSource)field!.GetValue(svc)!).Dispose();

        Assert.Null(Record.Exception(() => svc.Dispose()));

        Assert.Contains(events, log =>
            log.Level == LogEventLevel.Warning
            && log.MessageTemplate.Text.Contains(
                "Error during cancellation token source disposal in BiblePublicationScheduleService",
                StringComparison.Ordinal));

        Assert.Null(Record.Exception(() => svc.Dispose()));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var svc = new BiblePublicationScheduleService(new ScheduleTestScopeFactory(Options), TestLogging.CreateLogger());
        svc.Dispose();
        Assert.Null(Record.Exception(() => svc.Dispose()));
    }

    private static AlarmSchedule MinimalSchedule(string name) =>
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
            Music = new AlarmMusic { PublicationCode = "pub", TrackCode = "1", Repeat = false },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                TrackCode = "1",
                FinishedDuration = TimeSpan.Zero,
            },
        };

    private static AlarmSchedule MinimalScheduleWithoutBible(string name) =>
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
            Music = new AlarmMusic { PublicationCode = "pub", TrackCode = "1", Repeat = false },
            BiblePublicationSchedule = null,
        };
}
