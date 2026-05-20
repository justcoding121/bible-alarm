#nullable enable

using System.Reflection;
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

    [Fact]
    public void Dispose_swallows_errors_when_cancellation_source_cancel_fails()
    {
        var sut = new ScheduleMigrationService(TestLogging.CreateLogger());
        var field = typeof(ScheduleMigrationService).GetField(
            "cancellationTokenSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var disposed = new CancellationTokenSource();
        disposed.Dispose();
        field!.SetValue(sut, disposed);

        sut.Dispose();
    }
}
