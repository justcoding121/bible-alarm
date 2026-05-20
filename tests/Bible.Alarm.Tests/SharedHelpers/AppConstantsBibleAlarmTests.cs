#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Tests;

public sealed class AppConstantsBibleAlarmTests
{
    [Fact]
    public void SqliteAuxiliaryFileSuffixes_lists_wal_shm_and_journal_sidecars()
    {
        var suffixes = AppConstants.Database.SqliteAuxiliaryFileSuffixes;

        Assert.Equal(3, suffixes.Length);
        Assert.Equal(AppConstants.Database.SqliteWalFileSuffix, suffixes[0]);
        Assert.Equal(AppConstants.Database.SqliteShmFileSuffix, suffixes[1]);
        Assert.Equal(AppConstants.Database.SqliteJournalFileSuffix, suffixes[2]);
    }
}
