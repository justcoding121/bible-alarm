#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class OldMediaIndexDataCopierTests
{
    [Fact]
    public async Task CopyMissingAsync_NoOps_When_ScheduleDbMissing()
    {
        var sut = new OldMediaIndexDataCopier(TestLogging.CreateLogger());
        var tempDir = Path.Combine(Path.GetTempPath(), "bible-alarm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var oldDb = Path.Combine(tempDir, "old.db");
            var newDb = Path.Combine(tempDir, "new.db");
            await File.WriteAllTextAsync(oldDb, "");
            await File.WriteAllTextAsync(newDb, "");

            await sut.CopyMissingAsync(oldDb, newDb, Path.Combine(tempDir, "missing-schedule.db"));
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
            }
        }
    }
}
