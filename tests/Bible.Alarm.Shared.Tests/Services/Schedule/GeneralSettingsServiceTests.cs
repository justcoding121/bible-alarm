#nullable enable

using System.Reflection;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsServiceTests : IAsyncLifetime
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
    public void Constructor_ThrowsWhenScopeFactoryNull()
        => Assert.Throws<ArgumentNullException>(() =>
            new GeneralSettingsService(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_ThrowsWhenLoggerNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralSettingsService(new ScheduleTestScopeFactory(Options), null!));
    }

    [Fact]
    public async Task SetGetAndExist_Roundtrip()
    {
        using var service = CreateService();

        Assert.False(await service.GeneralSettingExistsAsync("alpha"));

        await service.SetGeneralSettingAsync("alpha", "1");
        Assert.True(await service.GeneralSettingExistsAsync("alpha"));

        var row = await service.GetGeneralSettingAsync("alpha");
        Assert.NotNull(row);
        Assert.Equal("alpha", row!.Key);
        Assert.Equal("1", row.Value);
    }

    [Fact]
    public async Task SetGeneralSetting_UpdatesExistingRow()
    {
        using var service = CreateService();

        await service.SetGeneralSettingAsync("k", "a");
        await service.SetGeneralSettingAsync("k", "b");

        var row = await service.GetGeneralSettingAsync("k");
        Assert.NotNull(row);
        Assert.Equal("b", row!.Value);
    }

    [Fact]
    public async Task GetGeneralSetting_When_KeyMissing_ReturnsNull()
    {
        using var service = CreateService();

        Assert.Null(await service.GetGeneralSettingAsync("__no_such_general_setting__"));
    }

    [Fact]
    public void Dispose_LogsWarning_When_CancellationTokenCleanupThrows()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var service = new GeneralSettingsService(new ScheduleTestScopeFactory(Options), logger);

        var field = typeof(GeneralSettingsService).GetField(
            "cancellationTokenSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        ((CancellationTokenSource)field!.GetValue(service)!).Dispose();

        Assert.Null(Record.Exception(() => service.Dispose()));

        Assert.Contains(events, log =>
            log.Level == LogEventLevel.Warning
            && log.MessageTemplate.Text.Contains(
                "Error during cancellation token source disposal in GeneralSettingsService",
                StringComparison.Ordinal));

        Assert.Null(Record.Exception(() => service.Dispose()));
    }

    [Fact]
    public void Dispose_IsIdempotent_OnServiceOnly()
    {
        var service = CreateService();
        service.Dispose();
        Assert.Null(Record.Exception(() => service.Dispose()));
    }

    private GeneralSettingsService CreateService() =>
        new(new ScheduleTestScopeFactory(Options), TestLogging.CreateLogger());
}
