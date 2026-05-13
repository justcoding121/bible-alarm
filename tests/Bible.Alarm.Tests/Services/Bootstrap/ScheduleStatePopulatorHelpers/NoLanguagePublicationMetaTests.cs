#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

namespace Bible.Alarm.Tests;

public sealed class NoLanguagePublicationMetaTests
{
    [Fact]
    public void Record_uses_value_semantics_on_all_positions()
    {
        var a = new LookupDataLoader.NoLanguagePublicationMeta("Instrumental", 9, "Music", true);
        var b = new LookupDataLoader.NoLanguagePublicationMeta("Instrumental", 9, "Music", true);

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { IsMusic = false });
        Assert.NotEqual(a, a with { CategoryId = 1 });
    }
}
