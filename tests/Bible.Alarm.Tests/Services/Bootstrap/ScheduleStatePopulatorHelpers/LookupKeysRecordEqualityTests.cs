#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

namespace Bible.Alarm.Tests;

public sealed class LookupKeysRecordEqualityTests
{
    [Fact]
    public void LookupKeys_supports_with_expression_without_changing_hash_sets()
    {
        var keys = LookupDataCollector.CollectKeys([]);
        Assert.Equal(keys, keys with { });
    }
}
