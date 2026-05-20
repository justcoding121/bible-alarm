#nullable enable

using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace Bible.Alarm.Tests;

public sealed class AlarmScheduleServiceBibleAlarmTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private DbContextOptions<Bible.Alarm.Shared.Database.ScheduleDbContext> Options =>
        new DbContextOptionsBuilder<Bible.Alarm.Shared.Database.ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

    public Task InitializeAsync()
    {
        connection.Open();
        using var bootstrap = new Bible.Alarm.Shared.Database.ScheduleDbContext(Options);
        bootstrap.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public void Constructor_throws_when_scope_factory_null()
    {
        Assert.Throws<ArgumentNullException>(() => new AlarmScheduleService(null!));
    }

    [Fact]
    public async Task AddGetUpdateDelete_and_queries_roundtrip()
    {
        using var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        var added = await sut.AddScheduleAsync(new AlarmSchedule
        {
            Name = "Morning",
            Hour = 7,
            Minute = 0,
        });

        Assert.True(added.Id > 0);
        Assert.True(await sut.ScheduleExistsAsync(added.Id));
        Assert.True(await sut.AnySchedulesExistAsync());

        var byId = await sut.GetScheduleByIdAsync(added.Id);
        Assert.NotNull(byId);
        Assert.Equal("Morning", byId!.Name);

        var all = await sut.GetAllSchedulesAsync();
        Assert.Single(all);

        var filtered = await sut.GetSchedulesAsync(s => s.Name == "Morning");
        Assert.Single(filtered);

        added.Name = "Updated";
        var updated = await sut.UpdateScheduleAsync(added);
        Assert.Equal("Updated", updated.Name);

        var updatedById = await sut.UpdateScheduleByIdAsync(added.Id, s => s.Minute = 15);
        Assert.Equal(15, updatedById.Minute);

        await sut.DeleteScheduleAsync(added.Id);
        Assert.False(await sut.ScheduleExistsAsync(added.Id));
    }

    [Fact]
    public async Task DeleteScheduleAsync_no_ops_when_schedule_missing()
    {
        using var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        await sut.DeleteScheduleAsync(scheduleId: 9999);

        Assert.False(await sut.AnySchedulesExistAsync());
    }

    [Fact]
    public async Task GetFirstScheduleOrDefault_returns_null_when_empty()
    {
        using var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        Assert.Null(await sut.GetFirstScheduleOrDefaultAsync());
    }

    [Fact]
    public async Task GetMusicAndBiblePublication_by_schedule_id()
    {
        using var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        var schedule = await sut.AddScheduleAsync(new AlarmSchedule
        {
            Name = "With children",
            Music = new AlarmMusic
            {
                PublicationCode = "iam",
                TrackCode = "1",
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                SectionCode = "1",
                TrackCode = "1",
            },
        });

        var music = await sut.GetMusicByScheduleIdAsync(schedule.Id);
        var bible = await sut.GetBiblePublicationByScheduleIdAsync(schedule.Id);

        Assert.NotNull(music);
        Assert.Equal("iam", music!.PublicationCode);
        Assert.NotNull(bible);
        Assert.Equal("nwt", bible!.PublicationCode);
    }

    [Fact]
    public async Task SaveChangesAsync_returns_zero_when_no_pending_changes()
    {
        using var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        Assert.Equal(0, await sut.SaveChangesAsync());
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void Dispose_swallows_cancellation_token_cleanup_errors()
    {
        var sut = new AlarmScheduleService(new ScheduleTestScopeFactory(Options));

        var field = typeof(AlarmScheduleService).GetField(
            "cancellationTokenSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var cts = new ThrowOnDisposeCancellationTokenSource();
        field.SetValue(sut, cts);

        var ex = Record.Exception(() => sut.Dispose());

        Assert.Null(ex);
        Assert.True(cts.IsCancellationRequested);
    }

    private sealed class ThrowOnDisposeCancellationTokenSource : CancellationTokenSource
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new ObjectDisposedException(nameof(ThrowOnDisposeCancellationTokenSource));
            }

            base.Dispose(disposing);
        }
    }
}
