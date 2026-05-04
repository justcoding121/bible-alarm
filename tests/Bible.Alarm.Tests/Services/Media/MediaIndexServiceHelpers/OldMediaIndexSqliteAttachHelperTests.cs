#nullable enable

using Bible.Alarm.Services.Media.MediaIndexServiceHelpers;
using Microsoft.Data.Sqlite;

namespace Bible.Alarm.Tests;

public sealed class OldMediaIndexSqliteAttachHelperTests
{
    [Fact]
    public async Task AttachOldMediaDatabaseAsync_throws_when_file_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_missing.db");
        await using var primary = new SqliteConnection("Data Source=:memory:");
        await primary.OpenAsync();

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            OldMediaIndexSqliteAttachHelper.AttachOldMediaDatabaseAsync(primary, missing));

        Assert.Equal(Path.GetFullPath(missing), ex.FileName);
    }

    [Fact]
    public async Task AttachOldMediaDatabaseAsync_attaches_existing_database()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bible-alarm-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var oldDbPath = Path.Combine(tempDir, "old_media.db");
        try
        {
            await using (var bootstrap = new SqliteConnection($"Data Source={oldDbPath}"))
            {
                await bootstrap.OpenAsync();
            }

            await using var primary = new SqliteConnection("Data Source=:memory:");
            await primary.OpenAsync();

            await OldMediaIndexSqliteAttachHelper.AttachOldMediaDatabaseAsync(primary, oldDbPath);

            await using var check = primary.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM {OldMediaIndexSqliteAttachHelper.OldMediaAlias}.sqlite_master;";
            var count = Convert.ToInt64(await check.ExecuteScalarAsync());
            Assert.True(count >= 0);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup on Windows handles.
            }
        }
    }
}
