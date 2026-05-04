using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class UrlHelperTests
{
    [Fact]
    public void JwOrgIndexServiceBaseUrl_MatchesCanonicalConstant()
    {
        Assert.Equal(AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl, UrlHelper.JwOrgIndexServiceBaseUrl);
        Assert.False(string.IsNullOrWhiteSpace(UrlHelper.JwOrgIndexServiceBaseUrl));
    }
}
