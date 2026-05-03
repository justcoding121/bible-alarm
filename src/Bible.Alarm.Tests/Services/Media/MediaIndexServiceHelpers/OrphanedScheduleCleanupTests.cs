#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class OrphanedScheduleCleanupTests
{
    [Fact]
    public async Task CleanupAsync_returns_immediately_when_database_paths_missing()
    {
        var sut = new OrphanedScheduleCleanup(TestLogging.CreateLogger());

        await sut.CleanupAsync(
            Path.Combine(Path.GetTempPath(), $"missing-media-{Guid.NewGuid():N}.db"),
            Path.Combine(Path.GetTempPath(), $"missing-sched-{Guid.NewGuid():N}.db"));
    }
}
