#nullable enable

using Bible.Alarm.Services.Database;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleMigrationServiceTests
{
    [Fact]
    public async Task MigrateBibleGatewaySchedulesAsync_completes_without_work()
    {
        using var sut = new ScheduleMigrationService(TestLogging.CreateLogger());

        await sut.MigrateBibleGatewaySchedulesAsync();
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = new ScheduleMigrationService(TestLogging.CreateLogger());

        sut.Dispose();
        sut.Dispose();
    }
}
