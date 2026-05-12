#nullable enable

using Bible.Alarm.Shared.Services.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class UrlConstructionLookUpPathCacheKeyTests
{
    [Fact]
    public void LookUpPathCacheKeyEqualityComparer_ignores_case_for_publication_language_section()
    {
        var cmp = UrlConstructionService.LookUpPathCacheKeyEqualityComparer.Instance;
        var a = new UrlConstructionService.LookUpPathCacheKey("Nwt", "e", "Gen", "01");
        var b = new UrlConstructionService.LookUpPathCacheKey("nwt", "E", "gen", "01");

        Assert.True(cmp.Equals(a, b));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(b));
    }

    [Fact]
    public void LookUpPathCacheKeyEqualityComparer_matches_track_code_with_ordinal_case_sensitivity()
    {
        var cmp = UrlConstructionService.LookUpPathCacheKeyEqualityComparer.Instance;
        var lower = new UrlConstructionService.LookUpPathCacheKey("nwt", "E", "gen", "a");
        var upper = new UrlConstructionService.LookUpPathCacheKey("nwt", "E", "gen", "A");

        Assert.False(cmp.Equals(lower, upper));
    }
}
