#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class UrlHelperBibleAlarmTests
{
    [Fact]
    public void JwOrgIndexServiceBaseUrl_matches_app_constant()
    {
        Assert.Equal(AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl, UrlHelper.JwOrgIndexServiceBaseUrl);
    }
}
