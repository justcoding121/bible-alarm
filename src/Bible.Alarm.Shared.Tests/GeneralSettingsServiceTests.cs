#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
    public void Dispose_IsIdempotent_OnServiceOnly()
    {
        var service = CreateService();
        service.Dispose();
        Assert.Null(Record.Exception(() => service.Dispose()));
    }

    private GeneralSettingsService CreateService() =>
        new(new ScheduleTestScopeFactory(Options), TestLogging.CreateLogger());
}
