using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class SourceWebsiteTests
{
    [Fact]
    public void Canonical_source_is_zero()
    {
        Assert.Equal(0, (int)SourceWebsite.JwOrg);
    }
}
